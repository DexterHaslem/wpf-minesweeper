using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace wpf_minesweeper
{
    internal enum GameState
    {
        Unset,
        Initialized,
        InProgress,
        Bombed,
        Winner,
    }

    internal class GameRules
    {
        private Stopwatch       _elapsedTime;

        // config state (cannot be changed w/o creating a new game)
        internal int BombCount    { get; private set; }
        internal int Rows         { get; private set; }
        internal int Columns      { get; private set; }

        internal TimeSpan ElapsedGameTime
        {
            get {  return _elapsedTime != null ? _elapsedTime.Elapsed : new TimeSpan(); }
        }

        internal GameState GameState { get; private set; }
        internal ObservableCollection<FieldSpot> GameField { get; private set; }

        //private static FieldSpot FieldAt(IEnumerable<FieldSpot> fields, int row, int col) { return fields != null ? fields.FirstOrDefault(f => f.Row == row && f.Column == col) : null;  }
        //private FieldSpot FieldAt(int row, int col) { return GameField != null ? GameField.FirstOrDefault(f => f.Row == row && f.Column == col) : null;  }

        private static void SpreadSomeBombs(GameRules gameRules)
        {
            Debug.Assert(gameRules != null);
            Random rand = new Random((int)DateTime.Now.Ticks); // loss of precision is intentional here
            List<Point> usedLocations = new List<Point>();

            for (int i = 0; i < gameRules.BombCount; i++)
            {
                Point newBombPoint;
                while (usedLocations.Contains(newBombPoint = new Point(rand.Next(gameRules.Rows), rand.Next(gameRules.Columns))) && usedLocations.Count < gameRules.BombCount)
                {
                    // generate a unique point
                }

                int scalarX = (int)newBombPoint.X;
                int scalarY = (int)newBombPoint.Y;
                usedLocations.Add(newBombPoint);
                var field =  gameRules.GameField.FirstOrDefault(f => f.Row == scalarX && f.Column == scalarY);
                if (field != null)
                    field.IsBomb = true;
            }
        }

        private static IEnumerable<FieldSpot> CreateNewField(int rows, int cols)
        {
            for (int x = 0; x < rows; x++)
                for (int y = 0; y < cols; y++)
                    yield return new FieldSpot { Row = x, Column = y };
        }

        private GameRules() { }

        internal static GameRules NewGame(int rows, int cols, int bombCount)
        {
            // lets fill out a new chunk of state for someone
            var newGameRules = new GameRules 
            {
                GameField  = new ObservableCollection<FieldSpot>(CreateNewField(rows, cols)),
                BombCount   = bombCount, 
                Rows        = rows, 
                Columns     = cols 
            };
            SpreadSomeBombs(newGameRules);
            newGameRules.GameState = GameState.Initialized;
            return newGameRules;
        }

        private int GetNeighborBombCount(FieldSpot spot)
        {
            if (spot.CachedBombNeighborCount != null)
                return spot.CachedBombNeighborCount.Value;
            return 0;
        }

        internal void SetTextForField(FieldSpot spot)
        {
            // note ordre is somewhat important
            if (spot.IsBomb)
                spot.Text = "X";
            else if (spot.IsFlagged)
                spot.Text = "F";
            else if (spot.IsCleared)
                spot.Text = "C";
            else 
                spot.Text = string.Empty;
        }

        internal void ClearSpot(FieldSpot spot)
        {
            spot.IsCleared = true;
            SetTextForField(spot);
            UpdateGameState();
        }

        internal void FlagSpot(FieldSpot spot)
        {
            spot.IsFlagged = !spot.IsFlagged;
            SetTextForField(spot);
            UpdateGameState();
        }

        private void UpdateGameState()
        {
            // if any bombs = pressed, user lost, if all bomb spots = flagged and all non bomb spots = cleared, user wins
            if (GameState < GameState.InProgress)
            { 
                GameState = wpf_minesweeper.GameState.InProgress;
                _elapsedTime = new Stopwatch();
                _elapsedTime.Start();
                // note: continue to run game logic here, it coulda been their first click
            }

            if (GameField.Any(f => f.IsCleared && f.IsBomb))
            {
                // user dun goofed
                GameState = GameState.Bombed;
                _elapsedTime.Stop();
                return;
            }

            if (GameField.Where(f => f.IsBomb).All(f => f.IsFlagged) && GameField.Where(f => !f.IsBomb).All(f => f.IsCleared))            
            {
                GameState = GameState.Winner;
                _elapsedTime.Stop();
            }
            else 
                GameState = GameState.InProgress;
        }

        internal int GetClearedCount()      {   return GameField != null ? GameField.Where(f => f.IsCleared).Count() : 0;                   }
        internal int GetRemainingCount()    {   return  GameField != null ? GameField.Count - GetClearedCount() - GetFlaggedCount() : 0;    }
        internal int GetFlaggedCount()      {   return  GameField != null ? GameField.Where(f => f.IsFlagged).Count() : 0;                  }
    }
}
