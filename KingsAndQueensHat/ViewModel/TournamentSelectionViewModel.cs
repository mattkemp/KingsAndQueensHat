﻿using KingsAndQueensHat.Annotations;
using KingsAndQueensHat.Persistence;
using KingsAndQueensHat.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace KingsAndQueensHat.ViewModel
{
    public class TournamentSelectionViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<PersistedTournament> Tournaments { get; private set; }

        public bool NoTournaments { get { return !Tournaments.Any(); } }

        public TournamentSelectionViewModel()
        {
            var baseDir = Constants.StorageDirectory();
            Directory.CreateDirectory(baseDir);
            var directories = Directory.EnumerateDirectories(baseDir);
            Tournaments = new ObservableCollection<PersistedTournament>(directories.Select(d => new PersistedTournament(Path.GetFileName(d))));
            foreach (var tourney in Tournaments)
            {
                tourney.Delete += DeleteTourney;
                tourney.Open += OpenTourney;
            }
            TournamentName = Enumerable.Range(1, int.MaxValue).Select(i => string.Format("New Tournament {0}", i)).FirstOrDefault(name => !Tournaments.Any(t => string.Equals(name, t.Name, StringComparison.CurrentCultureIgnoreCase)));
        }

        public event EventHandler Open;

        void OpenTourney(object sender, EventArgs e)
        {
            Open(sender, e);
        }

        void DeleteTourney(object sender, EventArgs e)
        {
            var tourney = sender as PersistedTournament;
            Tournaments.Remove(tourney);
            Directory.Delete(tourney.Path, recursive:true);
            OnPropertyChanged("NoTournaments");
        }

        public string TournamentName { get; set; }

        internal bool CanCreateTournament()
        {
            return !Tournaments.Any(t => t.Name.Equals(TournamentName, StringComparison.CurrentCultureIgnoreCase));
        }

        public TournamentPersistence GetStorageLocator(string name)
        {
            return new TournamentPersistence(name);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
