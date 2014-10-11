﻿using KingsAndQueensHat.ViewModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace KingsAndQueensHat.View
{
    /// <summary>
    /// Interaction logic for TournamentSelectionWindow.xaml
    /// </summary>
    public partial class TournamentSelectionWindow : Window
    {
        public TournamentSelectionWindow()
        {

            ViewModel = new TournamentSelectionViewModel();
            ViewModel.Open += ViewModel_Open;
            DataContext = ViewModel;
            InitializeComponent();
        }

        void ViewModel_Open(object sender, EventArgs e)
        {
            var tourney = sender as PersistedTournament;
            OpenTournament(ViewModel.GetStorageLocator(tourney.Name));
        }

        public TournamentSelectionViewModel ViewModel { get; private set; }

        private void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ViewModel.CanCreateTournament())
            {

            }
            OpenTournament(ViewModel.GetStorageLocator(ViewModel.TournamentName));
        }

        private void OpenTournament(Persistence.StorageLocator storageLocator)
        {
            var window = new MainWindow();
            window.SetStorageLocator(storageLocator);
            window.Show();
            Close();
        }
    }
}
