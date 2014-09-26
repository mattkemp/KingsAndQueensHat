﻿using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Documents;
using KingsAndQueensHat.Persistence;
using KingsAndQueensHat.TeamGeneration;
using System.IO;
using System.Xml;
using System;

namespace KingsAndQueensHat.Model
{
    /// <summary>
    /// Root of the model
    /// </summary>
    public class Tournament
    {
        public Tournament()
        {
            PlayerProvider = new PlayerFileReader(@"TestData.csv");
            Players = new ObservableCollection<Player>(PlayerProvider.Players.OrderByDescending(p => p.Skill));
            Rounds = new ObservableCollection<TeamSet>();
            Teams = new ObservableCollection<Team>();
        }

        private IPlayerProvider PlayerProvider { get; set; }

        public ObservableCollection<Player> Players { get; private set; }

        public ObservableCollection<TeamSet> Rounds { get; private set; }
        public ObservableCollection<Team> Teams { get; private set; }

        public void LoadExistingData()
        {
            Rounds.Clear();
            _playerPairings.Clear();

            var files = Directory.EnumerateFiles(".", "*.hatround");
            foreach (var file in files)
            {
                try
                {
                    var doc = new XmlDocument();
                    using (var stream = new FileStream(file, FileMode.Open))
                    {
                        doc.Load(stream);
                    }
                    var teams = doc.SelectNodes("/TeamSet/Teams/Team");
                    var teamList = new List<Team>();
                    foreach (XmlNode team in teams)
                    {
                        Team t = new Team();
                        var players = team.SelectNodes("Players/Player");
                        foreach (XmlNode player in players)
                        {
                            var name = player.SelectSingleNode("Name").InnerText;
                            Player p = PlayerProvider.PlayerWithName(name);
                            if (p != null)
                            {
                                t.AddPlayer(p);
                            }
                        }
                        GameResult gameResult;
                        if (Enum.TryParse<GameResult>(team.SelectSingleNode("GameResult").InnerText, out gameResult))
                        {
                            if (gameResult != GameResult.NoneYet)
                            {
                                t.GameDone(gameResult);
                            }
                        }

                        teamList.Add(t);
                    }
                    var round = new TeamSet(teamList);
                    Rounds.Add(round);
                    round.AddRoundToPairingCount(_playerPairings);
                }
                catch (Exception)
                {
                    // Let's just be overly lenient here
                }
            }
        }

        /// <summary>
        /// A record of who has played with whom
        /// </summary>
        private PlayerPairings _playerPairings = new PlayerPairings();

        public void CreateNewRound()
        {
            var teamCreator = new RoundCreator();
            var penalty1 = new UnevenSkillPenalty();
            var penalty3 = new TooManyWinnersPenalty(PlayerProvider);
            var penalties = new IPenalty[] { penalty1, _playerPairings, penalty3 };

            var teams = teamCreator.CreateApproximatelyOptimalTeams(penalties, PlayerProvider);
            
            Rounds.Add(teams);
            teams.AddRoundToPairingCount(_playerPairings);
            string filename;
            int i = 1;
            do
            {
                filename = string.Format("{0}.hatround", i);
                i++;
            } while (File.Exists(filename));

            using (var stream = new FileStream(filename, FileMode.Create))
            {
                teams.SaveToFile(stream);
            }

            Teams.Clear();
            foreach (Team team in teams.Teams)
            {
                Teams.Add(team);
            }
        }
    }
}