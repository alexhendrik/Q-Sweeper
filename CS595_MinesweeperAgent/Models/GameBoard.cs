/*
    Author: Oleksandr Hendrik 
    Project: Q-Sweeper

    This class defines the game board generation and functionality
    necessary to support the agent's interactions with the game.
 */

using System;
using System.Collections.Generic;
using System.Text;
using static CS595_MinesweeperAgent.Models.Enums;

namespace CS595_MinesweeperAgent.Models
{
    public class GameBoard
    {
        //       row  col
        private List<List<Tile>> Board = new List<List<Tile>>();
        private List<(int, int)> PossibleNextStates = new List<(int, int)>();

        public int Width { get; set; }
        public int Height { get; set; }

        private int BombCount { get; set; }

        private int UnrevealedCount { get; set; }
        public int DisplayCount { get; set; }

        public GameBoard(int width, int height, int numBombs)
        {
            Width = width;
            Height = height;
            BombCount = DisplayCount = numBombs;
            UnrevealedCount = Width * Height;

            // Initiate Board

            PopulateBoard();

            SetAdjacencies();

            RevealInit();

            PrintBoard();
        }

        //Board Operations

        // Reveal initial blank tile recursively.
        private void RevealInit(int attempt = 0)
        {
            var rand = new Random();

            int x = rand.Next(0, Width);
            int y = rand.Next(0, Height);

            if (!Board[y][x].IsBomb && Board[y][x].AdjacentCount == 0 || attempt > 5) // Prevent stack overflow
                RevealTile((x, y));
            else
                RevealInit(attempt + 1);
        }

        // Visual output
        public void PrintBoard()
        {
            foreach (var row in Board)
            {
                foreach (var tile in row)
                    if (tile.IsFlagged)
                        Console.Write("[P]");
                    else if (!tile.IsRevealed)
                        Console.Write("[ ]");
                    else if (tile.IsBomb)
                        Console.Write("[*]");
                    else
                        Console.Write("[{0}]", tile.AdjacentCount);
                Console.WriteLine();
            }
            Console.WriteLine();
        }

        // Generate the tiles for the board and place bombs.
        private void PopulateBoard()
        {

            for (int i = 0; i < Height; i++)
            {
                Board.Add(new List<Tile>());

                for (int j = 0; j < Width; j++)
                {
                    Board[i].Add(new Tile());
                }
            }

            var rand = new Random();

            for (int i = 0; i < BombCount; i++)
            {
                int x = rand.Next(0, Width);
                int y = rand.Next(0, Height);

                if (!Board[y][x].IsBomb)
                    Board[y][x].IsBomb = true;
                else
                    i--;
            }
        }

        // Update the tile objects with the number of adjacent bombs.
        // Gross 4 nested for-loops up ahead.
        private void SetAdjacencies()
        {
            for (int rowN = 0; rowN < Board.Count; rowN++)
            {
                for (int colN = 0; colN < Board[rowN].Count; colN++)
                {
                    if(!Board[rowN][colN].IsBomb)
                    {
                        Board[rowN][colN].AdjacentCount = 0;
                        for (int i = rowN - 1; i < rowN + 2; i++)
                        {
                            for (int j = colN - 1; j < colN + 2; j++)
                            {
                                if (ValidateCoordinates(j, i))
                                    if (Board[i][j].IsBomb)
                                        Board[rowN][colN].AdjacentCount++;

                            }
                        }
                    }
                }
            }
        }

        // Are you winning son?
        public bool CheckWinState()
        {
            return DisplayCount == BombCount && (BombCount == 0 || UnrevealedCount == BombCount);
        }

        public List<(int, int)> GetPossibleStates()
        {
            return new List<(int, int)>(PossibleNextStates); // Clone the list object to avoid external changes
        }

        public void RemovePossibleState((int, int) state)
        {
            PossibleNextStates.Remove(state);
        }

        // Ensure that a set of coordinates is within bounds
        public bool ValidateCoordinates(int x, int y)
        {
            return !(x < 0 || x >= Height || y < 0 || y >= Width);
        }

        // Kind of a redundant overload that I didn't have time to make the only one.
        public bool ValidateCoordinates((int x, int y) coords)
        {
            return !(coords.x < 0 || coords.x >= Height || coords.y < 0 || coords.y >= Width);
        }

        // Reveal a given tile and return the status of the reveal (see Enums.cs)
        public RevealResponse RevealTile((int x, int y) coords)
        {
            var tile = Board[coords.y][coords.x];

            if (tile.IsFlagged || tile.IsRevealed)
                return RevealResponse.Nothing;

            tile.IsRevealed = true;

            if (tile.IsBomb)
            {
                return RevealResponse.Bomb;
            }

            // If revealing a 0 - reveal all adjacent zeros and their immediate surroundings recursively
            if (tile.AdjacentCount == 0)
            {
                for (int i = 0; i < 3; i++)
                {
                    for (int j = 0; j < 3; j++)
                    {
                        if (ValidateCoordinates(coords.x + j - 1, coords.y + i - 1))
                            RevealTile((coords.x + j - 1, coords.y + i - 1));
                    }
                }
            }
            else
            {
                PossibleNextStates.Add(coords);
            }

            UnrevealedCount--;

            return RevealResponse.Success;
        }

        // Change to take in a tuple of x and y
        public bool FlagTile(int x, int y)
        {
            var tile = Board[y][x];

            if (tile.IsRevealed)
                return false;

            if(tile.IsFlagged)
            {
                tile.IsFlagged = false;
                if (tile.IsBomb)
                    BombCount++;
                DisplayCount++;
            }
            else
            {
                tile.IsFlagged = true;
                if (tile.IsBomb)
                    BombCount--;
                DisplayCount--;
            }

            return true;
        }

        // Check if all of the adjacent tiles for a state have been revealed
        public void UpdatePossibleState((int x, int y) state)
        {
            bool explored = true;
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    if (ValidateCoordinates(state.x + j - 1, state.y + i - 1))
                    {
                        if(!Board[state.y + i - 1][state.x + j - 1].IsRevealed)
                        {
                            explored = false;
                        }
                    }
                }
            }

            if (explored) RemovePossibleState(state);
        }

        // Change to take in a tuple of x and y
        // Return the local state context, change the default of the size parameter to adjust the size of the local state
        public Tile[][] GetLocalState(int x, int y, int size = 3)
        {
            var state = new Tile[size][];

            int offset = (int)(size / 2); // always returns an even number

            for (int i = 0; i < size; i++)
            {
                var row = new Tile[size];

                for (int j = 0; j < size; j++)
                {
                    if(ValidateCoordinates(x + j - offset, y + i - offset))
                        row[j] = Board[y + i - offset][x + j - offset];
                }
                state[i] = row;
            }

            return state;
        }

        public double GetPercentageCleared()
        {
            double tileCount = Width * Height;
            return (tileCount - UnrevealedCount - BombCount) / tileCount;
        }
    }
}
