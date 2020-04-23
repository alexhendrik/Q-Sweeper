/*
    Author: Oleksandr Hendrik 
    Project: Q-Sweeper

    This class defines the tiles that make up the game board.
 */


namespace CS595_MinesweeperAgent.Models
{
    public class Tile
    {
        public int AdjacentCount { get; set; }
        public bool IsBomb { get; set; }
        public bool IsFlagged { get; set; }
        public bool IsRevealed { get; set; }

        public Tile()
        {
            AdjacentCount = -1;
            IsBomb = IsFlagged = IsRevealed = false;
        }
    }
}
