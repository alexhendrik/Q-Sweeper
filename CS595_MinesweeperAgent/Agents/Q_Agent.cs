/*
    Author: Oleksandr Hendrik 
    Project: Q-Sweeper

    This class defines the agent responsible for training and testing the Q-Matrices
 */

using CS595_MinesweeperAgent.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using static CS595_MinesweeperAgent.Models.Enums;

namespace CS595_MinesweeperAgent.Agents
{
    public class Q_Agent
    {
        private static string MODEL_PATH = "trained_model.json";

        // [0][1][2]
        // [3][X][4]
        // [5][6][7]

        // [8]  [9]  [10] [11] [12]
        // [13] [0]  [1]  [2]  [14]
        // [15] [3]  [X]  [4]  [16]
        // [17] [5]  [6]  [7]  [18]
        // [19] [20] [21] [22] [23]

        private static readonly Dictionary<int, (int x_offset, int y_offset)> NODE_LAYOUT = new Dictionary<int, (int, int)>()
        { 
            // Hardcoding == bad
            { 0, (-1, -1) },
            { 1, (0, -1) },
            { 2, (1, -1) },
            { 3, (-1, 0) },
            { 4, (1, 0) },
            { 5, (-1, 1) },
            { 6, (0, 1) },
            { 7, (1, 1) },
            { 8, (-2, -2) },
            { 9, (-1, -2) },
            { 10, (0, -2) },
            { 11, (1, -2) },
            { 12, (2, -2) },
            { 13, (-2, -1) },
            { 14, (2, -1) },
            { 15, (-2, 0) },
            { 16, (2, 0) },
            { 17, (-2, 1) },
            { 18, (2, 1) },
            { 19, (-2, 2) },
            { 20, (-1, 2) },
            { 21, (0, 2) },
            { 22, (1, 2) },
            { 23, (2, 2) },
        };

        internal GameBoard gameBoard;
        internal Func<GameBoard> ResetFunc { get; set; } // Function to create a new board instance

        private int NumEpisodes { get; set; }
        private int NumTests { get; set; } = 1000;
        private double LearningRate { get; set; } = 0.50;
        private Dictionary<string, Dictionary<int, double>> LocalQ { get; set; } //State > Tile to reveal > reward

        public Q_Agent(Func<GameBoard> reset, int numEpisodes = 10000000)
        {
            NumEpisodes = numEpisodes;
            ResetFunc = reset;
            gameBoard = ResetFunc.Invoke();
        }

        // Maybe introduce overall fitness based on how many bombs were actually left?

        public void TrainLocal()
        {
            LocalQ = new Dictionary<string, Dictionary<int, double>>();
            int numWins = 0;

            Random rand = new Random();

            for (int i = 0; i < NumEpisodes; i++)
            {
                double temperature = 0.5;
                List<(string, int)> moveHistory = new List<(string, int)>();

                List<(int x, int y)> revealedTiles = new List<(int x, int y)>();

                GameOutcome episodeResult = GameOutcome.Undefined;
                bool endState = false;
                while (!endState)
                {
                    (int, int) selectedState;

                    var nextStates = gameBoard.GetPossibleStates();

                    if (nextStates.Count == 0)
                    {
                        Console.WriteLine("Ran out of possible states!");
                        episodeResult = GameOutcome.Undefined;
                        endState = true;
                        continue;
                    }

                    // Intelligently select the new state and get its hash code
                    selectedState = SelectNextState(nextStates);
                    string localStateHash = GetMatrixHashCode(gameBoard.GetLocalState(selectedState.Item1, selectedState.Item2));

                    // Add to matrix if not already in there
                    if (!LocalQ.ContainsKey(localStateHash))
                    {
                        AddStateToMatrix(localStateHash);
                    }

                    (int, double) bestMove = (0, LocalQ[localStateHash][0]);
                    foreach (var kvp in LocalQ[localStateHash])
                    {
                        // Compare with the addition of the temperature value
                        if (kvp.Value + temperature * kvp.Value >= bestMove.Item2) // can cause unexpected results, should split into multiple if statements
                        {
                            // 50% chance if the reward is the same
                            if (rand.NextDouble() < 0.5 && kvp.Value == bestMove.Item2) continue;

                            var converted = ConvertAction(selectedState, kvp.Key);
                            if (gameBoard.ValidateCoordinates(converted) && !revealedTiles.Contains(converted))
                                bestMove = (kvp.Key, kvp.Value);
                        }
                    }

                    moveHistory.Add((localStateHash, bestMove.Item1));
                    RevealResponse response = RevealResponse.Nothing;

                    var convertedBestAction = ConvertAction(selectedState, bestMove.Item1);

                    // Attempt to reveal the tile
                    if(gameBoard.ValidateCoordinates(convertedBestAction))
                    {
                        response = gameBoard.RevealTile(convertedBestAction);
                        revealedTiles.Add(convertedBestAction);
                    }

                    gameBoard.PrintBoard();

                    // Modify the reward/punishment values
                    switch (response)
                    {
                        case RevealResponse.Success:
                            LocalQ[localStateHash][bestMove.Item1] += LearningRate * 1;
                            break;
                        case RevealResponse.Nothing:
                            LocalQ[localStateHash][bestMove.Item1] -= LearningRate * 0.75;
                            break;
                        case RevealResponse.Bomb:
                            LocalQ[localStateHash][bestMove.Item1] -= LearningRate * 1;
                            episodeResult = GameOutcome.Loss;
                            endState = true;
                            break;
                    }

                    if (gameBoard.CheckWinState())
                    {
                        episodeResult = GameOutcome.Win;
                        endState = true;
                        numWins++;
                    }

                    temperature -= temperature * 0.25; // Cooling off
                }

                ModifyBasedOnOutcome(moveHistory, episodeResult);

                gameBoard = ResetFunc.Invoke();
                SaveTrainedModel();
            }
        }

        // Should merge this with TrainModel() using lambdas
        // Code is almost identical to TrainModel()
        public void TestModel()
        {
            if(!LoadTrainedModel()) throw new Exception("The model was not found at the expected path!");

            int numWins = 0;

            double winRate = 0;
            List<double> clearedRates = new List<double>();
            Dictionary<int, int> stepStatistics = new Dictionary<int, int>();

            Random rand = new Random();

            for (int i = 0; i < NumTests; i++)
            {
                bool endState = false;
                int moveCount = 0;
                
                while (!endState)
                {
                    // skip this board if it gets stuck
                    if(moveCount > 200)
                    {
                        endState = true;
                        continue;
                    }

                    (int, int) selectedState;

                    List<(int x, int y)> revealedTiles = new List<(int x, int y)>();

                    var nextStates = gameBoard.GetPossibleStates();

                    if (nextStates.Count == 0)
                    {
                        Console.WriteLine("Ran out of possible states!");
                        endState = true;
                        continue;
                    }

                    selectedState = SelectNextState(nextStates);

                    string localStateHash = GetMatrixHashCode(gameBoard.GetLocalState(selectedState.Item1, selectedState.Item2));

                    (int, double) bestMove;

                    if (!LocalQ.ContainsKey(localStateHash))
                    {
                        bestMove = (rand.Next(0, 8), 0.0); 
                    }
                    else
                    {
                        bestMove = (0, LocalQ[localStateHash][0]);

                        foreach (var kvp in LocalQ[localStateHash])
                        {
                            if (kvp.Value >= bestMove.Item2) 
                            {
                                var converted = ConvertAction(selectedState, kvp.Key);
                                if (gameBoard.ValidateCoordinates(converted) && !revealedTiles.Contains(converted)) // Make it go to a different state if all tiles are revealed
                                    bestMove = (kvp.Key, kvp.Value);
                            }
                        }
                    }

                    RevealResponse response = RevealResponse.Nothing;

                    var convertedBestAction = ConvertAction(selectedState, bestMove.Item1);

                    if (gameBoard.ValidateCoordinates(convertedBestAction))
                    {
                        response = gameBoard.RevealTile(convertedBestAction);
                        revealedTiles.Add(convertedBestAction);
                        
                    }

                    gameBoard.PrintBoard();

                    if (response == RevealResponse.Bomb) endState = true;

                    if (gameBoard.CheckWinState())
                    {
                        endState = true;
                        numWins++;
                        Console.WriteLine("Win!!!");
                    }

                    moveCount++;
                }

                winRate = 100 * numWins / (i + 1);
                Console.WriteLine(string.Format("Win Count: {0} out of {1} ({2}%)", numWins, (i + 1), winRate));

                clearedRates.Add(gameBoard.GetPercentageCleared());

                if (stepStatistics.ContainsKey(moveCount))
                    stepStatistics[moveCount]++;
                else
                    stepStatistics.Add(moveCount, 1);

                gameBoard = ResetFunc.Invoke();
            }

            Console.WriteLine("Testing is complete.  Step stats: ");
            Console.WriteLine(JsonConvert.SerializeObject(stepStatistics));
            Console.WriteLine("Average percentage cleared: " + clearedRates.Average());
            Console.ReadLine();
        }

        // Add the encountered state to the Q matrix and populate all of the action dictionary entries for it with 0s
        private void AddStateToMatrix(string stateHash)
        {
            LocalQ.Add(stateHash, new Dictionary<int, double>());
            for (int i = 0; i < stateHash.Length - 1; i++)
            {
                LocalQ[stateHash].Add(i, 0.0);
            }
        }

        // Modify the reward of every past move based on the game being won or lost
        private void ModifyBasedOnOutcome(List<(string, int)> moves, GameOutcome outcome)
        {
            var modifier = 0.0;
            switch (outcome)
            {
                case GameOutcome.Loss:
                    modifier = -0.1;
                    break;
                case GameOutcome.Win:
                    modifier = 0.1;
                    break;
                case GameOutcome.Undefined:
                    break;
            }

            foreach (var move in moves)
            {
                LocalQ[move.Item1][move.Item2] += modifier; 
            }
        }

        private void SaveTrainedModel()
        {

            var modelJson = JsonConvert.SerializeObject(LocalQ);

            File.WriteAllText(MODEL_PATH, modelJson);
        }

        private bool LoadTrainedModel()
        {
            if (!File.Exists(MODEL_PATH)) return false;

            var modelText = File.ReadAllText(MODEL_PATH);

            LocalQ = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<int, double>>>(modelText);

            return true;
        }

        // Use the layout mapping to convert an action and a state to a set of coordinates
        private (int, int) ConvertAction((int x, int y) coords, int action)
        {
            var offsets = NODE_LAYOUT[action];
            return (coords.x + offsets.x_offset, coords.y + offsets.y_offset);
        }

        // Hash the matrix together
        private string GetMatrixHashCode(Tile[][] matrix)
        {
            var center = matrix.Length / 2;
            string hash = matrix[center][center].AdjacentCount.ToString();

            for (int i = 0; i < (matrix.Length * matrix.Length) - 1; i++)
            {
                var offsets = NODE_LAYOUT[i];
                var tile = matrix[center + offsets.y_offset][center + offsets.x_offset];

                if(tile != null)
                {
                    // Cheese Mode
                    //hash += tile.IsBomb ? 1 : 0;
                    //hash += tile.IsFlagged ? 1 : 0;
                    hash += tile.IsRevealed ? 1 : 0;
                }
                else
                {
                    hash += "x";
                }
            }

            return hash;
        }

        // Determine the next state with the highest combined reward
        private (int x, int y) SelectNextState(List<(int x, int y)> states)
        {
            (int x, int y) selection = (-1, -1);
            double? selectionScore = null;

            // Gets the job done
            foreach (var state in states)
            {
                double scoreSum = 0;
                var dictKey = GetMatrixHashCode(gameBoard.GetLocalState(state.x, state.y));

                if (LocalQ.ContainsKey(dictKey))
                {
                    var scores = LocalQ[dictKey];

                    foreach (var score in scores)
                    {
                        scoreSum += score.Value;
                    }
                }

                // This is quite bad code duplication but the null check needs to happen separately from the comparison.
                if (selectionScore == null)
                {
                    selection = state;
                    selectionScore = scoreSum;
                }
                else if (scoreSum > selectionScore)
                {
                    selection = state;
                    selectionScore = scoreSum;
                }
            }

            return selection;
        }
    }
}
