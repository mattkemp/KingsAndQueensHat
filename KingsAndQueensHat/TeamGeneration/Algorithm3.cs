using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KingsAndQueensHat.Model;
using KingsAndQueensHat.Properties;

namespace KingsAndQueensHat.TeamGeneration
{
	class Algorithm3
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
					}
				}
			}

			// calculate adjusted scores
			var averageScore =0M;
			var playersWithAtLeastOneGame = playerProvider.AllPlayers.Where(x => x.GamesPlayed > 0).ToList();
			if(playersWithAtLeastOneGame.Count > 0) averageScore = Convert.ToDecimal(playersWithAtLeastOneGame.Average(x => x.GameScore));
			foreach (var player in playerProvider.AllPlayers)
			{
				// adjusted score is so that:
				// - players who have missed games, but are good (high win%) don't get treated like bad players
				// - we average the adjusted scores for the handicapping of players getting a long way ahead
				
				var experienceFactor = player.SkillLevel.Value / 100M; // ends up being something like: .2, .5, .6, .8, 1
				if (player.GamesPlayed == 0 && rounds.Count() > 1) experienceFactor += experienceFactor; // double the effect if they haven't played any games yet, and we are into the league (not round 1)
				var winPercentFactor = (player.WinPercent - 50M) / 100; //  this a negative factor for win % < 50, positive for > 50
				var winsAndGamesPlayed =  winPercentFactor * player.GamesPlayed; // if they haven't played any games - makes no difference

				var adjustedScore = averageScore + experienceFactor + winsAndGamesPlayed;
				player.AdjustedScore = Math.Max(player.GameScore, adjustedScore); // whichever is higher
			}

			var averageAdjustedScore = 0M;
			if (playersWithAtLeastOneGame.Count > 0) averageAdjustedScore = Convert.ToDecimal(playersWithAtLeastOneGame.Average(x => x.AdjustedScore));

			_presentPlayers = Sort(playerProvider.PresentPlayers().ToList());
			Log("Games played so far: " + rounds.Count() + "\n");
			foreach (var player in _presentPlayers) {
				// figure out handicap
				var handicapPower = 1.2; // i.e. 7 points above average = 7^1.2 = handicap
				var diffToAverage = player.AdjustedScore - averageAdjustedScore;
				player.Handicap = diffToAverage >= 0
					? (decimal)Math.Pow((double)diffToAverage, handicapPower)
					: (decimal)Math.Pow((double)Math.Abs(diffToAverage), handicapPower) * -1;
				
				// logging
				Log(String.Format("{0}\t{4}\tPlayed:{6}\tPoints/Adj:{5}/{1}\tHCap:{7}\tWin:{2}%\tXP:{3}"
					, player.Name.PadRight(20)
					, player.AdjustedScore.ToString("0.00").PadLeft(5)
					, player.WinPercent.ToString("0").PadLeft(3)
					, player.SkillLevel.Value.ToString().PadLeft(3)
					, player.Gender
					, player.GameScore.ToString().PadLeft(2)
					, player.GamesPlayed.ToString().PadLeft(2)
					, player.Handicap.ToString("0.00").PadLeft(5)
					));
			}
		}

		private void Log(string lineToWrite)
		{
			if (LoggingOn) File.AppendAllText(LoggingPath, lineToWrite + Environment.NewLine);
		}

		private List<Team> DistributePlayers() {

			// work out how many players will be in each team up front
			Log("");
			SetEachTeamsNumberOfPlayersToAssign(Gender.Male, true);
			SetEachTeamsNumberOfPlayersToAssign(Gender.Female, true);
			Log("");

			var evenNumberedRound = Rounds.Count % 2 == 1;

			if (_teams.Count > 2 && EvenRoundsGroupBest && evenNumberedRound) {
				// every second round, put all the best players in the first two teams, then distribute the rest as normal

				// reduce the team list to just two, so everything else works as normal
				var remainingTeams = _teams.ToList(); // copy it
				_teams.RemoveRange(2,_teams.Count - 2);
				remainingTeams.RemoveRange(0,2);

				do {
					// take top x men and distribute
					AssignTeam(Sort(_presentPlayers.Where(x => x.Gender == Gender.Male).ToList()), true);
					// take top x women and distribute
					AssignTeam(Sort(_presentPlayers.Where(x => x.Gender == Gender.Female).ToList()), true);
				} while (_teams.Any(x=> !x.IsAllFull));

				// then put back the remaining teams and continue distributing as normal
				_teams.AddRange(remainingTeams);
			}

			// normal team distribution
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
			var seed = int.Parse(DateTime.Now.ToString("yyyyMMdd")); // seed with today so can reproduce when testing
			var r = new Random(seed); // tested and this works
			if (topFirst)
			{
				availablePlayers = availablePlayers
					.OrderByDescending(x => x.AdjustedScore)
					.ThenByDescending(x => x.WinPercent)
					.ThenByDescending(x => x.SkillLevel.Value)
					.ThenByDescending(x => r.Next()) // so it isn't just alphabetic with all other things being equal
					.ToList();
			} else
			{
				availablePlayers = availablePlayers
					.OrderBy(x => x.AdjustedScore)
					.ThenBy(x => x.WinPercent)
					.ThenBy(x => x.SkillLevel.Value)
					.ThenBy(x => r.Next()) // so it isn't just alphabetic with all other things being equal
					.ToList();
			}

			return availablePlayers;
		}

		private void AssignTeam(List<Player> players, bool topFirst) // no defaults on these params to make it more obvious
		{
			// given the list of players, this will assign at least one to each team, but based on handicap often one will get a couple
			// stops when each team has been given at least one

			// reset the counter
			foreach (var team in _teams) {
				team.PlayersAssignedThisLoop = 0;
			}

			// some initialisation
			if (!players.Any()) return;
			var gender = players.First().Gender;

			do {
				if (!players.Any()) return;
				var thisPlayer = players.First();

				// if teams are full then break out
				if(_teams.All(x => x.IsFull(gender))) return;

				var team = GetNextTeam(topFirst, gender);

				var previousTotalHandicap = team.HandicapTotal;
				team.AddPlayer(thisPlayer);
				Log(String.Format("{0} => T{1} {2:0.00} {3}{4:0.00} = {5:0.00}"
					, thisPlayer.Name
					, team.Number
					, previousTotalHandicap
					, thisPlayer.Handicap >= 0 ? "+" : ""
					, thisPlayer.Handicap
					, team.HandicapTotal
					));
				
				team.PlayersAssignedThisLoop++;

				players.Remove(thisPlayer); // remove from the temp list we are working with now
				_presentPlayers.Remove(thisPlayer); // remove from the master player list

			} while (_teams.Any(x => x.PlayersAssignedThisLoop == 0));
		}

		private Team GetNextTeam(bool topFirst, Gender gender) {
			return topFirst ? 
				_teams.Where(x=>x.IsNotFull(gender)).OrderBy(x => x.HandicapTotal).ThenBy(x=>x.Number).First() : 
				_teams.Where(x=>x.IsNotFull(gender)).OrderByDescending(x => x.HandicapTotal).ThenBy(x=>x.Number).First();
		}

		private void SetEachTeamsNumberOfPlayersToAssign(Gender gender, bool shouldFirstTeamsHaveMorePlayers) {
			var logTeamNumbers = "";
			
			for (int i = 0; i < _teams.Count; i++)
			{
				var playersRemaining = _presentPlayers.Count(x=>x.Gender==gender) 
										- _teams.Sum(x => gender==Gender.Male ? x.NumberOfMenToAssign : x.NumberOfWomenToAssign);

				var preciseNumberPerTeam = (decimal)playersRemaining / (_teams.Count-i);

				var forThisTeam = shouldFirstTeamsHaveMorePlayers
					? (int)Math.Ceiling(preciseNumberPerTeam)
					: (int)Math.Floor(preciseNumberPerTeam);

				if (gender == Gender.Male) {
					_teams[i].NumberOfMenToAssign = forThisTeam;
					logTeamNumbers += String.Format("T{0}: {1}M, ", i+1, forThisTeam);
				}
				else {
					_teams[i].NumberOfWomenToAssign = forThisTeam;
					logTeamNumbers += String.Format("T{0}: {1}F, ", i+1, forThisTeam);
				}
			}
			Log(logTeamNumbers);
		}
	}
}
