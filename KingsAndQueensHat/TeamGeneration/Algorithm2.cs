﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KingsAndQueensHat.Model;
using KingsAndQueensHat.Properties;

namespace KingsAndQueensHat.TeamGeneration
{
    class Algorithm2
    {

        private List<Player> _presentPlayers;
        private List<Team> _teams;
        public bool LoggingOn { get; set; }
        public string LoggingPath { get; set; }
        public bool EvenRoundsGroupBest { get; set; }
        public List<HatRound> Rounds { get; set; }

        public List<Team> Generate(IPlayerProvider playerProvider, int numTeams)
        {
            PopulateRoundResults(Rounds, playerProvider);

            // create teams and distribute players
            _teams = Enumerable.Range(0, numTeams)
                .Select(i => new Team(i+1))
                .ToList();
            return DistributePlayers();
        }

        private void PopulateRoundResults(IEnumerable<HatRound> rounds, IPlayerProvider playerProvider)
        {

            Log(String.Format("\nLog time: {0}", DateTime.Now.ToString("dd MMM yyyy HH:mm:ss")));

            // reset the values
            foreach (var player in playerProvider.AllPlayers)
            {
                player.GamesPlayed = 0;
                player.NumberOfWins = 0;
                player.NumberOfDraws = 0;
                player.NumberOfLosses = 0;
            }

            // calculate win percentages
            foreach (var hatRound in rounds)
            {
                foreach (var team in hatRound.Teams)
                {
                    foreach (var player in team.Players)
                    {
                        var p = playerProvider.AllPlayers.First(x => x == player);
                        if (p == null) continue;
                        p.GamesPlayed++;
                        if (team.GameResult == GameResult.Won) p.NumberOfWins++;
                        if (team.GameResult == GameResult.Draw) p.NumberOfDraws++;
                        if (team.GameResult == GameResult.Lost) p.NumberOfLosses++;
                    }
                }
            }

            // calculate adjusted scores
            var averageScore =0M;
            var playersWithAtLeastOneGame = playerProvider.AllPlayers.Where(x => x.GamesPlayed > 0).ToList();
            if(playersWithAtLeastOneGame.Count > 0) averageScore = Convert.ToDecimal(playersWithAtLeastOneGame.Average(x => x.GameScore));
            foreach (var player in playerProvider.AllPlayers)
            {
                // adjusted score is so that players who have missed games, but are good (high win%) don't get treated like bad players
                // start with the average of the whole field
                // based on win percent factor adjust the score up or down
                // score moves by factor times number of games played

                var experienceFactor = player.SkillLevel.Value / 100M;
                if (player.GamesPlayed == 0 && rounds.Count() > 1) experienceFactor += experienceFactor; // double the effect if they haven't played any games yet, and we are into the league (not round 1)
                var winPercentFactor = (player.WinPercent - 50M) / 100; //  this a negative factor for win % < 50, positive for > 50
                var adjustedScore = averageScore + experienceFactor + winPercentFactor * player.GamesPlayed; // if they haven't played any games - makes no difference
                player.AdjustedScore = Math.Max(player.GameScore, adjustedScore);
            }

            _presentPlayers = Sort(playerProvider.PresentPlayers().ToList());
            Log("Games played so far: " + rounds.Count() + "\n");
            foreach (var player in _presentPlayers)
            {
                Log(String.Format("{0}\t{4}\tPlayed:{6}\tPoints/Adj:{5}/{1}\tWin:{2}%\tXP:{3}"
                    , player.Name.PadRight(15)
                    , player.AdjustedScore.ToString("0.00").PadLeft(5)
                    , player.WinPercent.ToString("0").PadLeft(3)
                    , player.SkillLevel.Value.ToString().PadLeft(3)
                    , player.Gender
                    , player.GameScore.ToString().PadLeft(2)
                    , player.GamesPlayed.ToString().PadLeft(2)
                    ));
            }
        }

        private void Log(string lineToWrite)
        {
            if (LoggingOn) File.AppendAllText(LoggingPath, lineToWrite + Environment.NewLine);
        }

        private List<Team> DistributePlayers() {
            var aboutToMakeAnEvenNumberRound = Rounds.Count % 2 == 1;

            if (_teams.Count > 2 && EvenRoundsGroupBest && aboutToMakeAnEvenNumberRound) {
                // every second round, put all the best players in the first two teams, then distribute the rest as normal
                
                var howManyPeopleForTheseTeams = Math.Floor((decimal)_presentPlayers.Count / _teams.Count);

                // reduce the team list to just two, so everything else works as normal
                var remainingTeams = _teams.ToList(); // copy it
                _teams.RemoveRange(2,_teams.Count - 2);
                remainingTeams.RemoveRange(0,2);

                do {
                    // take top x men and distribute
                    AssignTeam(Sort(_presentPlayers.Where(x => x.Gender == Gender.Male).ToList()), true);
                    // take top x women and distribute
                    AssignTeam(Sort(_presentPlayers.Where(x => x.Gender == Gender.Female).ToList()), true);
                } while (_teams.Min(x => x.PlayerCount) < howManyPeopleForTheseTeams);

                // then put back the remaining teams and continue distributing as normal
                _teams.AddRange(remainingTeams);
            }

            do {
                // take top x men and distribute
                AssignTeam(Sort(_presentPlayers.Where(x => x.Gender == Gender.Male).ToList()), true);
                // take top x women and distribute
                AssignTeam(Sort(_presentPlayers.Where(x => x.Gender == Gender.Female).ToList()), true);
                // take bottom x men and distribute
                AssignTeam(Sort(_presentPlayers.Where(x => x.Gender == Gender.Male).ToList(), false), false);
                // take bottom x women and distribute
                AssignTeam(Sort(_presentPlayers.Where(x => x.Gender == Gender.Female).ToList(), false), false);

            } while (_presentPlayers.Any());

            return _teams;
        }

        private static List<Player> Sort(List<Player> availablePlayers, bool topFirst = true)
        {
            var r = new Random();
            if (topFirst)
            {
                availablePlayers = availablePlayers
                    .OrderByDescending(x => x.AdjustedScore)
                    .ThenByDescending(x => x.WinPercent)
                    .ThenByDescending(x => x.SkillLevel.Value)
                    .ThenByDescending(x => r.Next()) // not easily testable, but this is so it isn't just alphabetic with all other things being equal
                    .ToList();
            } else
            {
                availablePlayers = availablePlayers
                    .OrderBy(x => x.AdjustedScore)
                    .ThenBy(x => x.WinPercent)
                    .ThenBy(x => x.SkillLevel.Value)
                    .ThenBy(x => r.Next()) // not easily testable, but this is so it isn't just alphabetic with all other things being equal
                    .ToList();
            }

            return availablePlayers;
        }

        private void AssignTeam(List<Player> players, bool topFirst) // no defaults on these params to make it more obvious
        {
            if (!players.Any()) return;
            var gender = players.First().Gender;
            var aboutToRunOutOfPlayers = players.Count <= _teams.Count;

            for (var i = 0; i < _teams.Count; i++) { // if we have four teams, run through four times - it *should* be even teams after a round of this, but sometimes isn't (which is ok)
                if (!players.Any()) return;
                var thisPlayer = players.First();

                GetNextTeam(topFirst, gender, aboutToRunOutOfPlayers).AddPlayer(thisPlayer);

                players.Remove(thisPlayer); // remove from the temp list we are working with now
                _presentPlayers.Remove(thisPlayer); // remove from the master player list
            }
        }

        private Team GetNextTeam(bool topFirst, Gender gender, bool aboutToRunOutOfPlayers)
        {
            // the main idea is to get top ranked players always facing off against each other


            // if teams.Count isn't even we might need to do a couple of things:
            //  - skip the last team when re-ordering. We need the two top women on opposite teams
            //  - even gender split section might need some re-work?

            // make a copy of teams and whittle down to best suited team
            var orderedTeams = _teams.ToList();
            if (topFirst && gender == Gender.Female)
            {
                // TODO: if teams.Count isn't even skip the last team? Need the two top women on even teams
                orderedTeams.Reverse(); // flip the sort order - so top women aren't on same team as top men
            }
            var whittledTeams = orderedTeams.ToList();

            // Even gender split is most important factor
            if (aboutToRunOutOfPlayers) {
                
                var maxGenderOnATeam = orderedTeams.Max(x => x.OfGender(gender));
                var allTeamsHaveTheSameNumberOfGender = orderedTeams.All(x => x.OfGender(gender) == maxGenderOnATeam);
                if (!allTeamsHaveTheSameNumberOfGender) {
                    // teams are in pairs 1&2, 3&4 etc

                    // look ahead for uneven pairs first - main priority
                    var thereAreUnevenPairings = false;
                    for (int i = 0; i + 1 < orderedTeams.Count; i = i + 2)
                    {
                        var thisTeam = orderedTeams[i];
                        var nextTeam = orderedTeams[i + 1];

                        thereAreUnevenPairings = thisTeam.OfGender(gender) != nextTeam.OfGender(gender);
                        if (thereAreUnevenPairings) break; // stop looking
                    }

                    // knock out teams we don't want to put even more players on
                    for (int i = 0; i + 1 < orderedTeams.Count; i = i + 2) {
                        var thisTeam = orderedTeams[i];
                        var nextTeam = orderedTeams[i + 1];

                        // which teams to whittle down?
                        if (thisTeam.OfGender(gender) == nextTeam.OfGender(gender)) {
                            if (thereAreUnevenPairings || thisTeam.OfGender(gender) == maxGenderOnATeam) { // both
                                whittledTeams.Remove(thisTeam);
                                whittledTeams.Remove(nextTeam);
                            }
                        }
                        if (thisTeam.OfGender(gender) > nextTeam.OfGender(gender)) whittledTeams.Remove(thisTeam); // thisTeam
                        if (thisTeam.OfGender(gender) < nextTeam.OfGender(gender)) whittledTeams.Remove(nextTeam); // nextTeam
                    }
                }
            }

            // TODO: put more players in first teams so that people can fill in if no-shows for later games


            // get team with lowest number of players, lowest points totals, (first round this is teams 1->X), then by lowest ranked captain (first player)

            // knock out teams with more players
            whittledTeams.RemoveAll(x => x.PlayerCount > whittledTeams.Min(y => y.PlayerCount));
            if (whittledTeams.Count == 1) return whittledTeams.First();

            if (topFirst)
            {
                // assigning top players - knock out teams with MORE adjusted points, because we need to help lowest team
                whittledTeams.RemoveAll(x => x.TotalAdjustedScore > whittledTeams.Min(y => y.TotalAdjustedScore));
                if (whittledTeams.Count == 1) return whittledTeams.First();

                // knock out higher ranked FirstPlayers - only works if at least one person has been assigned
                if (whittledTeams.Any(x => x.PlayerCount > 0))
                {
                    whittledTeams.RemoveAll(x => x.FirstPlayer != null && x.FirstPlayer.AdjustedScore > whittledTeams.Min(y => y.FirstPlayer.AdjustedScore));
                }
            }
            else
            {
                // assigning bottom players - knock out teams with LESS adjusted points, because we need to hinder(!) highest team
                whittledTeams.RemoveAll(x => x.TotalAdjustedScore < whittledTeams.Max(y => y.TotalAdjustedScore));
                if (whittledTeams.Count == 1) return whittledTeams.First();

                // knock out lowest ranked FirstPlayers - only works if at least one person has been assigned
                if (whittledTeams.Any(x => x.PlayerCount > 0))
                {
                    whittledTeams.RemoveAll(x => x.FirstPlayer != null && x.FirstPlayer.AdjustedScore < whittledTeams.Max(y => y.FirstPlayer.AdjustedScore));
                }
            }

            return whittledTeams.First();
        }
    }
}
