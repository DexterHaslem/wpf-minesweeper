using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace wpf_minesweeper
{
    public class NotBooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (bool) value ? Visibility.Collapsed : Visibility.Visible;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }

    public class NotifyPropertyChangedBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName]string name = null)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(name));
        }
    }

    public class FieldSpot : NotifyPropertyChangedBase
    {
        private string  _text;
        private bool    _isBomb;
        private bool    _isCleared;
        private bool    _isFlagged;

        public bool IsCleared
        {
            get { return _isCleared; }
            set
            {
                if (_isCleared == value)
                    return;
                _isCleared = value;
                OnPropertyChanged();
            }
        }

        public bool IsBomb                      
        { 
            get { return _isBomb; }
            set
            {
                if (_isBomb == value)
                    return;
                _isBomb = value; 
                OnPropertyChanged();
            }
        }

        public bool IsFlagged
        {
            get { return _isFlagged; }
            set
            {
                if (_isFlagged == value)
                    return;
                _isFlagged = value;
                OnPropertyChanged();
            }
        }

        public string Text
        {
            get { return _text; }
            set
            {
                if (_text == value)
                    return;
                _text = value;
                OnPropertyChanged();
            }
        }

        // gamestate stuff only
        public int?     CachedBombNeighborCount     { get; set; }
        
        public int      Row                         { get; set; }
        public int      Column                      { get; set; }
        public Point    Point { get { return new Point(Row, Column); } }
    }

    public class MineSweeperViewModel : NotifyPropertyChangedBase
    {
        private TimeSpan        _currentTime;
        private int             _columns;
        private int             _flaggedCount;
        private int             _difficulty;
        private int             _clearedCount;
        private int             _remainingCount;
        private bool            _isGameConfigEnabled;
        private MainView        _owner;
        private int             _rows;
        private int             _bombCount;
        private DispatcherTimer _uiTimer;
        private GameRules       _gameRules;

        private const int UI_UPDATE_RATE_MS = 50;

        //public static int MaxDifficulty = 10; // mostly for granularity of the slider on ui (scaled to percent of row*col)
       // public static int MaxFieldSize = 20; // max size of a row or column

        public int FlaggedCount
        {
            get { return _flaggedCount; }
            set
            {
                if (_flaggedCount == value)
                    return;
                _flaggedCount = value;
                OnPropertyChanged();
            }
        }

        public int ClearedCount
        {
            get { return _clearedCount; }
            set
            {
                if (_clearedCount == value)
                    return;
                _clearedCount = value;
                OnPropertyChanged();
            }
        }

        public int Difficulty
        {
            get { return _difficulty; }
            set
            {
                if (_difficulty == value)
                    return;
                _difficulty = value;
                OnPropertyChanged();
            }
        }

        public bool IsGameConfigEnabled
        {
            get { return _isGameConfigEnabled; }
            set
            {
                if (_isGameConfigEnabled == value)
                    return;
                _isGameConfigEnabled = value;
                OnPropertyChanged();
            }
        }

        public int RemainingCount
        {
            get { return _remainingCount; }
            set
            {
                if (_remainingCount == value)
                    return;
                _remainingCount = value;
                OnPropertyChanged();
            }
        }

        public TimeSpan CurrentTime
        {
            get { return _currentTime; }
            set
            {
                if (_currentTime == value)
                    return;
                _currentTime = value;
                OnPropertyChanged();
            }
        }

        public static RoutedCommand FieldSelectedCommand    = new RoutedCommand("FieldSelected", typeof(MineSweeperViewModel));
        public static RoutedCommand FieldFlaggedCommand     = new RoutedCommand("FieldFlagged", typeof(MineSweeperViewModel));
        public static RoutedCommand StartGameCommand        = new RoutedCommand("StartGame", typeof(MineSweeperViewModel));
        public static RoutedCommand ResetGameCommand        = new RoutedCommand("ResetGame", typeof(MineSweeperViewModel));

        public int BombCount
        {
            get { return _bombCount; }
            set
            {
                if (_bombCount == value)
                    return;
                _bombCount = value;
                OnPropertyChanged();
            }
        }

        public int Columns
        {
            get { return _columns; }
            set
            {
                if (_columns == value)
                    return;
                _columns = value;
                OnPropertyChanged();
            }
        }

        public int Rows
        {
            get { return _rows; }
            set
            {
                if (_rows == value)
                    return;
                _rows = value;
                OnPropertyChanged();
            }
        }

        public MineSweeperViewModel(MainView owner)
        {
            _owner = owner;
            _owner.CommandBindings.Add(new CommandBinding(FieldSelectedCommand, OnFieldSelected, CanFieldSelectExecute));
            _owner.CommandBindings.Add(new CommandBinding(FieldFlaggedCommand, OnFieldFlagged, CanFieldSelectExecute));
            _owner.CommandBindings.Add(new CommandBinding(StartGameCommand, OnStartGame));
            _owner.CommandBindings.Add(new CommandBinding(ResetGameCommand, OnForceResetGame));
            _uiTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(UI_UPDATE_RATE_MS), DispatcherPriority.Background, OnUiUpdateTimerTick, _owner.Dispatcher);
           
            // give some defaults here for initial window load to not be empty
            Difficulty = 1;
            Rows = 8;
            Columns = 8;
            BombCount = 5;
            _gameRules = GameRules.NewGame(Rows, Columns, BombCount);
            // kick off a fresh game
            ResetGame();
        }

        private void OnForceResetGame(object sender, ExecutedRoutedEventArgs e)
        {
            ResetGame();
        }

        private void OnStartGame(object sender, ExecutedRoutedEventArgs e)
        {
            BombCount = (int)((Rows * Columns) * (Difficulty / 10d));
            _gameRules = GameRules.NewGame(Rows, Columns, BombCount);
            // HACK: force update crap (as well as before timer starts)
            OnPropertyChanged("MineField");
            IsGameConfigEnabled = false;
            CheckCurrentGameState();
        }

        public ObservableCollection<FieldSpot> MineField { get {  return _gameRules != null ? _gameRules.GameField : null; } }

        private void CheckCurrentGameState()
        {
            if (!_uiTimer.IsEnabled)
                _uiTimer.Start();
            // user still chugging away, not yet interested in taking action
            if (_gameRules.GameState != GameState.Winner && _gameRules.GameState != GameState.Bombed)
                return;
      
            string message = (_gameRules.GameState == GameState.Winner ? "You won! great work mate." : "You lost! Terrible luck.") + "  Play a new game?";
            if (MessageBox.Show(_owner, message, "Game over", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            { 
                _owner.Close();
                return;
            }

            ResetGame();
        }

        private void ResetGame()
        {
            _uiTimer.Stop();
            IsGameConfigEnabled = true;
            CurrentTime = new TimeSpan();
        }

        private void OnUiUpdateTimerTick(object sender, EventArgs e)
        {
            // chop the milliseconds off
            CurrentTime     = TimeSpan.FromSeconds((int)_gameRules.ElapsedGameTime.TotalSeconds);
            ClearedCount    = _gameRules.GetClearedCount();
            RemainingCount  = _gameRules.GetRemainingCount();
            FlaggedCount    = _gameRules.GetFlaggedCount();
        }

        private void CanFieldSelectExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
            return;
            /*
            if (IsGameConfigEnabled)
            {
                e.CanExecute = false;
                return;
            }

            var element = e.OriginalSource as FrameworkElement;
            if (element == null)
                return;
            var fieldSpot = element.DataContext as FieldSpot;
            e.CanExecute = fieldSpot != null && !fieldSpot.IsCleared && !fieldSpot.IsFlagged;*/
        }

        private void OnFieldFlagged(object sender, ExecutedRoutedEventArgs e)
        {
            var fieldSpot = e.Parameter as FieldSpot;
            if (fieldSpot == null)
                return;

            _gameRules.FlagSpot(fieldSpot);
            CheckCurrentGameState();
        }

        private void OnFieldSelected(object sender, ExecutedRoutedEventArgs e)
        {
            var fieldSpot = e.Parameter as FieldSpot;
            if (fieldSpot == null)
                return;
            _gameRules.ClearSpot(fieldSpot);
            CheckCurrentGameState();
        }
    }
}
