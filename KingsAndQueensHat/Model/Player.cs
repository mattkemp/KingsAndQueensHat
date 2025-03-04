﻿using System.ComponentModel;
using System.Runtime.CompilerServices;
using KingsAndQueensHat.Annotations;
using System;
using System.Xml.Serialization;
using KingsAndQueensHat.Utils;
using System.Collections.ObjectModel;
namespace KingsAndQueensHat.Model
{
    public class Player : INotifyPropertyChanged
    {
        private Func<Player, bool> _isWinning;
        private TournamentSettings _settings;
        public Player(string name, SkillLevel skill, Gender gender, bool currentlyPresent, TournamentSettings settings, Func<Player, bool> isWinning)
        {
            Name = name;
            SkillLevel = skill;
            Gender = gender;
            CurrentlyPresent = currentlyPresent;
            GameScore = 0;
            _settings = settings;
            _isWinning = isWinning;
        }

        /// <summary>
        /// For serialization
        /// </summary>
        protected Player()
        {

        }

        /// <summary>
        /// For deserialization
        /// </summary>
        internal void Rewire(TournamentSettings settings, Func<Player, bool> isWinning)
        {
            _settings = settings;
            _isWinning = isWinning;
        }

        public event EventHandler OnChange;
        public event EventHandler<PlayerEventArgs> OnRemoveFromRound;
        public event EventHandler<PlayerEventArgs> OnDelete;
        
        public string Name { get; set; }

        public Gender Gender { get; set; }

        [XmlIgnore]
        public SkillLevel SkillLevel 
        {
            get { return _settings.SkillLevel(Skill); }
            set
            {
                Skill = value.Name;
            }
        }
        
        private string _skill;
        public string Skill
        {
            get
            {
                return _skill;
            }
            set
            {
                if (value != _skill)
                {
                    _skill = value;
                    var @event = OnChange;
                    if (@event != null)
                    {
                        @event(this, new EventArgs());
                    }
                }
            }
        }

        private bool _currentlyPresent;
        public bool CurrentlyPresent
        {
            get { return _currentlyPresent; }
            set
            {
                if (value != _currentlyPresent)
                {
                    _currentlyPresent = value;
                    var @event = OnChange;
                    if (@event != null)
                    {
                        @event(this, new EventArgs());
                    }
                }
            }
        }

        [XmlIgnore]
        public int GameScore { get; private set; }

        [XmlIgnore]
        public bool PotentialMonarch
        {
            get
            {
                return _isWinning(this);
            }
        }

        // Algo2 extra properties
        [XmlIgnore]
        public decimal GamesPlayed { get; set; }
        [XmlIgnore]
        public int NumberOfWins { get; set; }
        [XmlIgnore]
        public int NumberOfDraws { get; set; }
        [XmlIgnore]
        public int NumberOfLosses { get; set; }
        [XmlIgnore]
        public decimal WinPercent { get { return GamesPlayed == 0 ? 0 : NumberOfWins/GamesPlayed*100; } }
        [XmlIgnore]
        public decimal AdjustedScore { get; set; }
        [XmlIgnore]
        public int RandomForSort { get; set; }
        [XmlIgnore]
        public decimal Handicap { get; set; }
		[XmlIgnore]
		public decimal HandicapPlusAdjusted => AdjustedScore + Handicap;

        public override string ToString()
        {
			// shows on the ad-hoc "add player to team" drop down on the Round screen
			// also used for logging and ease of debugging

			var winDrawLoss = $"({NumberOfWins}/{NumberOfDraws}/{NumberOfLosses})";
			return String.Format("{0} {4}  Played:{6} {10}  Win:{2}%  Points:{5}  Adj:{1}  HCap:{7}  XP:{3}  R:{9}"
				, Name.PadRight(20)
				, AdjustedScore.ToString("0.00").PadLeft(5)
				, WinPercent.ToString("0").PadLeft(3)
				, SkillLevel.Value.ToString().PadLeft(4)
				, Gender.ToString().PadRight(7)
				, GameScore.ToString().PadLeft(2)
				, GamesPlayed.ToString().PadLeft(2)
				, Handicap.ToString("0.00").PadLeft(6)
				, HandicapPlusAdjusted.ToString("0.00").PadLeft(6)
				, (RandomForSort/100000000M).ToString("0.0").PadLeft(5)
				, winDrawLoss.PadLeft(8)
			);
        }

        public void GameDone(GameResult gameResult, GameResult oldGameResult)
        {
            // Undo the old game result
            GameScore -= ScoreFor(oldGameResult);

            // Apply the new game result
            GameScore += ScoreFor(gameResult);

            OnPropertyChanged("GameScore");
        }

        private int ScoreFor(GameResult result)
        {
            switch (result)
            {
                case GameResult.Won:
                    return 3;
                case GameResult.Draw:
                    return 2;
                case GameResult.Lost:
                    return 1;
                default:
                    return 0;
            }
        }

        private CommandHandler _deleteCommand;
        public CommandHandler Delete
        {
            get
            {
                return _deleteCommand ?? (_deleteCommand = new CommandHandler(FireDelete, () => true));
            }
        }

        private void FireDelete()
        {
            var @event = OnDelete;
            if (@event != null)
            {
                @event(this, new PlayerEventArgs(this));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Hacky solution to the problem of updating our potential monarchitude
        /// </summary>
        internal void ForceUpdate()
        {
            OnPropertyChanged("PotentialMonarch");
        }

        private CommandHandler _removeSelfCommand;
        public CommandHandler RemoveSelfFromCurrentRound
        {
            get
            {
                return _removeSelfCommand ?? (_removeSelfCommand = new CommandHandler(() => HandleRemovePlayer(), () => true));
            }
        }

        public void HandleRemovePlayer()
        {
            var @event = OnRemoveFromRound;
            if (@event != null)
            {
                @event(this, new PlayerEventArgs(this));
            }
        }
    }
}
