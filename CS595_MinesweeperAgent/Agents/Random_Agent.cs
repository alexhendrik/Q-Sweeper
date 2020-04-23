/*
    Author: Oleksandr Hendrik 
    Project: Q-Sweeper

    This class defines the random agent and it's testing suite
 */

using CS595_MinesweeperAgent.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static CS595_MinesweeperAgent.Models.Enums;

namespace CS595_MinesweeperAgent.Agents
{
    public class Random_Agent : Q_Agent
    {
        private Random Rand;
        private int NumRuns { get; set; }

        // Should have a generic agent superclass, this inheritance is largely worthless due to protection levels
        public Random_Agent(Func<GameBoard> reset) : base(reset, 0)
        {
            Rand = new Random();
            NumRuns = 1000;
        }

        // Most of the code is very similar to TrainModel and TestModel in the Q_Agent, which is why I would like to combine all 3 into a single method with parametrized behavior
        public void TestRandom()
        {
            int winCount = 0;

            Dictionary<int, int> stepStatistics = new Dictionary<int, int>();
            List<double> clearedRates = new List<double>();

            for (int i = 0; i < NumRuns; i++)
            {
                bool endState = false;
                int stepCount = 0;

                while(!endState)
                {
                    (int, int) selectedState;
                    RevealResponse response = RevealResponse.Nothing;

                    var revealedTiles = new List<(int, int)>();

                    // Five attempts to find a new random state that has not already been revealed

                    int attempt = 0;
                    do
                    {
                        selectedState = SelectRandomState();
                        attempt++;
                        if(attempt > 200)
                        {
                            endState = true;
                            break;
                        }
                    } 
                    while (revealedTiles.Contains(selectedState));

                    if (gameBoard.ValidateCoordinates(selectedState))
                    {
                        response = gameBoard.RevealTile(selectedState);
                        revealedTiles.Add(selectedState);
                    }

                    gameBoard.PrintBoard();

                    if (response == RevealResponse.Bomb) endState = true;

                    if (gameBoard.CheckWinState())
                    {
                        endState = true;
                        winCount++;
                    }

                    stepCount++;
                }

                clearedRates.Add(gameBoard.GetPercentageCleared());

                if (stepStatistics.ContainsKey(stepCount))
                    stepStatistics[stepCount]++;
                else
                    stepStatistics.Add(stepCount, 1);

                gameBoard = ResetFunc.Invoke();
            }

            Console.WriteLine(string.Format("Win Count: {0} out of {1} ({2}%)", winCount, NumRuns, (winCount * 100 / NumRuns)));
            Console.WriteLine("Testing is complete.  Step stats: ");
            Console.WriteLine(JsonConvert.SerializeObject(stepStatistics));
            Console.WriteLine("Average percentage cleared: " + clearedRates.Average());
            Console.ReadLine();
        }

        // Select a completely random spot to reveal within the board bounds
        private (int x, int y) SelectRandomState()
        {
            return (Rand.Next(0, gameBoard.Width), Rand.Next(0, gameBoard.Height));
        }
    }
}
