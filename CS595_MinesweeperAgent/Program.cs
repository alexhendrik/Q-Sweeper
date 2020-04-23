/*
    Author: Oleksandr Hendrik 
    Project: Q-Sweeper

    This class defines the game board reset function and
    handles program arguments.
 */

using CS595_MinesweeperAgent.Agents;
using CS595_MinesweeperAgent.Models;
using System;
using System.Linq;

namespace CS595_MinesweeperAgent
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Func<GameBoard> startFunc = () => new GameBoard(5, 5, 5);

            // Future TODO: Add training model name and size as arguments

            if(args.Contains("-run"))
            {
                new Q_Agent(startFunc).TestModel();
            }
            else if (args.Contains("-rand"))
            {
                new Random_Agent(startFunc).TestRandom();
            }
            else if (args.Contains("-manual"))
            {
                // Gimmicky way of manual minesweeper playing
                var board = startFunc.Invoke();
                while (true)
                {
                    var x = int.Parse(Console.ReadLine());
                    var y = int.Parse(Console.ReadLine());
                    Console.WriteLine(board.RevealTile((x, y)).ToString());
                    board.PrintBoard();
                }
            }
            else
            {
                new Q_Agent(startFunc).TrainLocal();
            }
        }
    }
}
