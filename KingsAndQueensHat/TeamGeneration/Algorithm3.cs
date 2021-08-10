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
		public bool IsEvenRoundAndWeAreDoingBottomPlayersNow { get; set; }
		public List<HatRound> Rounds { get; set; }
		private const double HandicapPowerNumber = 1.2;

		public List<Team> Generate(IPlayerProvider playerProvider, int numTeams)
		{
			PopulateRoundResults(Rounds, playerProvider);

			// create teams and distribute players
			_teams = Enumerable.Range(0, numTeams)
				.Select(i => new Team(i+1))
				.ToList();

			DistributePlayers();
			
			return _teams;
		}

		private void PopulateRoundResults(IEnumerable<HatRound> rounds, IPlayerProvider playerProvider)
		{

			Log(String.Format("\nLog time: {0}", DateTime.Now.ToString("dd MMM yyyy HH:mm:ss")));

			// reset the values
			var seed = int.Parse(DateTime.Now.ToString("yyyyMMdd")); // seed with today so can reproduce when testing
			var r = new Random(seed); // tested and this works

			foreach (var player in playerProvider.AllPlayers)
			{
				player.GamesPlayed = 0;
				player.NumberOfWins = 0;
				player.RandomForSort = r.Next();
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

			_presentPlayers = Sort(playerProvider.PresentPlayers().ToList(), true);
			Log("Games played so far: " + rounds.Count() + "\n");
			foreach (var player in _presentPlayers) {
				// figure out handicap
				var diffToAverage = player.AdjustedScore - averageAdjustedScore;
				player.Handicap = diffToAverage >= 0
					? (decimal)Math.Pow((double)diffToAverage, HandicapPowerNumber)
					: (decimal)Math.Pow((double)Math.Abs(diffToAverage), HandicapPowerNumber) * -1;
				
				Log(player.ToString());
			}
		}

		private void Log(string lineToWrite)
		{
			if (LoggingOn) File.AppendAllText(LoggingPath, lineToWrite + Environment.NewLine);
		}

		private void DistributePlayers() {

			var evenNumberedRound = Rounds.Count % 2 == 1;
			var doGroupBestPlayersInFirstTwoTeams = _teams.Count > 2 && EvenRoundsGroupBest && evenNumberedRound;

			// work out how many players will be in each team
			SetEachTeamsNumberOfPlayersToAssign(doGroupBestPlayersInFirstTwoTeams);

			if (doGroupBestPlayersInFirstTwoTeams) {
				// every second round, put all the best players in the first two teams, then distribute the rest as normal
				// fixes the "I never play with Riley" issue where top 4 people would always be split otherwise
				// and can make a more fun game for people to be playing with similar levels (although it's really points based not ability)

				// reduce the team list to just two, so everything else works as normal
				var remainingTeams = _teams.ToList(); // copy it
				_teams.RemoveRange(2,_teams.Count - 2);
				remainingTeams.RemoveRange(0,2);

				// split out top players (so we can do top and bottom team assignment like normal)
				var topPlayers = new List<Player>();
				var remainingPlayers = new List<Player>();
				var totalMen = _teams.Sum(x => x.NumberOfMenToAssign);
				var totalWomen = _teams.Sum(x => x.NumberOfWomenToAssign);
				foreach (var player in Sort(_presentPlayers.ToList(), true)) {
					switch (player.Gender) {
						case Gender.Male:
							if (totalMen > 0) {
								topPlayers.Add(player);
								totalMen--;
							}
							else {
								remainingPlayers.Add(player);
							}

							break;
						case Gender.Female:
							if (totalWomen > 0) {
								topPlayers.Add(player);
								totalWomen--;
							}
							else {
								remainingPlayers.Add(player);
							}

							break;
					}
				}

				_presentPlayers = topPlayers;
				AssignTopAndBottom();

				// then put back the remaining teams and players and continue distributing as normal
				_teams.AddRange(remainingTeams);
				_presentPlayers = remainingPlayers;
				IsEvenRoundAndWeAreDoingBottomPlayersNow = true;
			}

			// normal team distribution
			AssignTopAndBottom();
		}

		private void AssignTopAndBottom() {
			do {				
				if (IsEvenRoundAndWeAreDoingBottomPlayersNow) {
					// weird first case 
					IsEvenRoundAndWeAreDoingBottomPlayersNow = false; // reset this

					AssignTeam(Sort(_presentPlayers.Where(x => x.Gender == Gender.Male).ToList(), false), true); // worst players, best teams
					AssignTeam(Sort(_presentPlayers.Where(x => x.Gender == Gender.Female).ToList(), false), true); // worst players, best teams
					AssignTeam(Sort(_presentPlayers.Where(x => x.Gender == Gender.Male).ToList(), true), false); // best players, worst teams
					AssignTeam(Sort(_presentPlayers.Where(x => x.Gender == Gender.Female).ToList(), true), false); // best players, worst teams
				}
				else {
					// normal
					AssignTeam(Sort(_presentPlayers.Where(x => x.Gender == Gender.Male).ToList(), true), false); // best players, worst teams
					AssignTeam(Sort(_presentPlayers.Where(x => x.Gender == Gender.Female).ToList(), true), false); // best players, worst teams
					AssignTeam(Sort(_presentPlayers.Where(x => x.Gender == Gender.Male).ToList(), false), true); // worst players, best teams
					AssignTeam(Sort(_presentPlayers.Where(x => x.Gender == Gender.Female).ToList(), false), true); // worst players, best teams
				}

				if (_presentPlayers.Count == 0 && _teams.Any(x => !x.IsAllFull))
				{
					throw new Exception("run out of players, but teams aren't full yet!");
				}

			} while (_teams.Any(x=> !x.IsAllFull));
		}

		private static List<Player> Sort(List<Player> availablePlayers, bool topFirst)
		{
			if (topFirst)
			{
				availablePlayers = availablePlayers
					.OrderByDescending(x => x.AdjustedScore)
					.ThenByDescending(x => x.WinPercent)
					.ThenByDescending(x => x.SkillLevel.Value)
					.ThenByDescending(x => x.RandomForSort) // so it isn't just alphabetic with all other things being equal
					.ToList();
			} else
			{
				availablePlayers = availablePlayers
					.OrderBy(x => x.AdjustedScore)
					.ThenBy(x => x.WinPercent)
					.ThenBy(x => x.SkillLevel.Value)
					.ThenBy(x => x.RandomForSort) // so it isn't just alphabetic with all other things being equal
					.ToList();
			}

			return availablePlayers;
		}

		private void AssignTeam(List<Player> players, bool bestFirst)
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

				var team = GetNextTeam(bestFirst, gender);
				if (team == null) {
					return;
				}

				var previousTotalHandicap = team.HandicapTotal;
				team.AddPlayer(thisPlayer);
				Log(String.Format("{0} => T{1} {2} {3}{4} ={5}"
					, thisPlayer.Name.PadRight(20)
					, team.Number
					, previousTotalHandicap.ToString("0.00").PadLeft(5)
					, thisPlayer.Handicap >= 0 ? "+" : "-"
					, Math.Abs(thisPlayer.Handicap).ToString("0.00").PadRight(5)
					, team.HandicapTotal.ToString("0.00").PadLeft(6)
					));
				
				team.PlayersAssignedThisLoop++;

				players.Remove(thisPlayer); // remove from the temp list we are working with now
				_presentPlayers.Remove(thisPlayer); // remove from the master player list

			} while (_teams.Any(x => !x.IsAllFull && x.PlayersAssignedThisLoop == 0));
		}

		private Team GetNextTeam(bool bestTeamsFirst, Gender gender) {
			// find first team that isn't full, sort by handicap, team number
			// bestTeamsFirst normally means we are assigning a bad player to the best teams
			// when assigning good players to bad teams (bestTeamsFirst=false) we only put one on each team (see && x.PlayersAssignedThisLoop == 0)
			// because we want top ladies versing each other, we reverse sort on the Team Number to make sure that happens (if handicapTotals are same)

			var ladyReverseSort = (gender == Gender.Female) ? -1 : 1;

			return bestTeamsFirst ? 
				_teams.Where(x=>x.IsNotFull(gender)).OrderByDescending(x => x.HandicapTotal).ThenBy(x=> x.Number).FirstOrDefault() : 
				_teams.Where(x=>x.IsNotFull(gender) && x.PlayersAssignedThisLoop == 0).OrderBy(x => x.HandicapTotal).ThenBy(x=>x.Number*ladyReverseSort).FirstOrDefault();
		}

		private void SetEachTeamsNumberOfPlayersToAssign(bool doGroupBestPlayersInFirstTwoTeams) {
			
			SetEachTeamsGenderSpecificNumberOfPlayersToAssign(Gender.Male);
			SetEachTeamsGenderSpecificNumberOfPlayersToAssign(Gender.Female);
			
			var playersWithTopAtTheTop = Sort(_presentPlayers.ToList(), true);
			var numberOfPlayersInFirstTwoTeams = _teams.Take(2).Sum(x=>x.TotalNumberToAssign);
			var topPlayers = playersWithTopAtTheTop.Take(numberOfPlayersInFirstTwoTeams);
			var isMoreMenInTopSectionOfPlayers = topPlayers.Count(x=>x.Gender == Gender.Male) > topPlayers.Count(x=>x.Gender == Gender.Female);

			// we want even player numbers per teams (even if that means gender isn't even), the
			// method "SetEachTeamsGenderSpecificNumberOfPlayersToAssign" above puts more in the first teams
			// each pair should be the same if possible
			// we are only ever moving one person off a team, because the initial allocation is normally pretty close

			#region Tests
			/*
example: - T1 too many, T4 too few
	(24 players - precise=6, best=6):
	T1 4M 3F
	T2 3M 3F
	T3 3M 3F
	T4 3M 2F
	=> should move M from T1 to T4

example: - T1 too many, T4 too few
	(21 players - precise=5.25, best=5)
	T1 3M 3F
	T2 3M 3F
	T3 3M 2F
	T4 2M 2F
	=> should be:
	T1 2M 3F => one M off
	T2 2M 3F => one M off (T1 and T2 are even now)
	T4 3M 2F => one M on
	...next loop...
	T3 3M 2F => one M on (after T4 gets one - due to desc team number sort)

example: T4 too few
	(22 players - precise=5.5, best=6)
	T1 3M 3F
	T2 3M 3F
	T3 3M 3F
	T4 2M 2F
	=> should be:
	T1 3M 3F => leave as-is
	T2 3M 3F => leave as-is
	T3 2M 3F => one M off
	T4 3M 2F => one M on

example: - M are evenly spread already, only move an F
	(21 players - precise=5.25, best=5)
	T1 3M 3F
	T2 3M 2F
	T3 3M 2F
	T4 3M 2F
	=> should be:
	T1 3M 2F => one F off
	T2 3M 2F
	T3 3M 2F
	T4 3M 3F => one F on

example - even number of M's (10), not quite enough F (11)
	(21 players - precise=5.25, best=5)
	T1 3M 3F
	T2 3M 3F
	T3 2M 3F
	T4 2M 2F
	=> should be:
	T1 2M 3F => one M off
	T2 2M 3F => one M off
	T3 3M 3F => one M on
	T4 3M 2F => one M on
			
example - Even round so top half needed, 20 people (10M, 10F):
	T1 3M 3F
	T2 3M 3F
	T3 2M 2F
	T4 2M 2F
	=> if there are more men in top half of sorted players, then put more men in teams 1 & 2:
	T1 3M 2F => one F off
	T2 3M 2F => one F off
	T3 2M 3F => one F on
	T4 2M 3F => one F on

*/
			#endregion

			var preciseNumberPerTeam = (decimal)_presentPlayers.Count / _teams.Count;
			var genderToMove = Gender.Male;
			var numberOfPeopleToMove = 0;
			// we need to round down if precise has .5
			var bestNumberPerTeam = (preciseNumberPerTeam % 0.5M == 0) ? Math.Floor(preciseNumberPerTeam) : Math.Round(preciseNumberPerTeam);
			// if precise = 5.75 then most teams will have 6 people, but minority will have 5
			// if precise = 5.25 then most teams will have 5 people, but minority will have 6

			// do we have any teams with more than "best"?
			// take 1 off these teams (first one sets the gender we are moving - it's only ever one)

			// removals =====================================
			var isAllTeamsMenTheSame = _teams.All(x=>x.NumberOfMenToAssign == _teams[0].NumberOfMenToAssign);

			foreach (var team in _teams) {
				if (team.TotalNumberToAssign > bestNumberPerTeam) {
					// remove one (always only one)
					numberOfPeopleToMove++;

					if (doGroupBestPlayersInFirstTwoTeams && isMoreMenInTopSectionOfPlayers) {
						if (!isAllTeamsMenTheSame && team.NumberOfMenToAssign > team.NumberOfWomenToAssign) {
							team.NumberOfMenToAssign--;
						}
						else {
							genderToMove = Gender.Female; // doesn't change after first loop, so is ok
							team.NumberOfWomenToAssign--;
						}
					}							
					else {
						if (isAllTeamsMenTheSame || team.NumberOfWomenToAssign > team.NumberOfMenToAssign) {
							genderToMove = Gender.Female; // doesn't change after first loop, so is ok
							team.NumberOfWomenToAssign--;
						}
						else {
							team.NumberOfMenToAssign--;
						}
					}
				}
			}

			// while numberOfPeopleToMove > 0, add people back

			// additions (only if we have some) =====================================
			while (numberOfPeopleToMove > 0) {
				// first of all make sure pairs of teams have the same numbers
				var i = 0;
				while (i + 1 < _teams.Count)
				{
					var teamA = _teams[i];
					var teamB = _teams[i + 1];
					if (teamA.TotalNumberToAssign < teamB.TotalNumberToAssign)
					{
						AddToTeamNumbers(teamA, genderToMove);
						numberOfPeopleToMove--;
					}
					if (teamB.TotalNumberToAssign < teamA.TotalNumberToAssign)
					{
						AddToTeamNumbers(teamB, genderToMove);
						numberOfPeopleToMove--;
					}

					i += 2;
				}

				// I think at this point all teams are guaranteed to be even (needs testing)
				if(numberOfPeopleToMove == 0) break;
				// so if we have any left over, choose a team to break the tie and begin again making sure pairs match

				// start with the team with the least number of players
				// then if all equal, try to even up gender
				// then highest team number (T4) because if all else is equal, may as well be the last team
				var bestTeam = _teams.OrderBy(x => x.TotalNumberToAssign)
					.ThenBy(x=> genderToMove == Gender.Male ? x.NumberOfMenToAssign : x.NumberOfWomenToAssign)
					.ThenByDescending(x => x.Number).First();
				
				AddToTeamNumbers(bestTeam, genderToMove);
				numberOfPeopleToMove--;
			}

			// =====================================

			// logging
			Log("");
			foreach (var team in _teams) {
				Log($"T{team.Number} {team.NumberOfMenToAssign}M {team.NumberOfWomenToAssign}F");
			}
			Log("");

		}

		private void AddToTeamNumbers(Team team, Gender genderToMove) {
			if (genderToMove == Gender.Male) {
				team.NumberOfMenToAssign++;
			}
			else {
				team.NumberOfWomenToAssign++;
			}
		}

		private void SetEachTeamsGenderSpecificNumberOfPlayersToAssign(Gender gender) {
			for (int i = 0; i < _teams.Count; i++)
			{
				var playersRemaining = _presentPlayers.Count(x=>x.Gender==gender) 
										- _teams.Sum(x => gender==Gender.Male ? x.NumberOfMenToAssign : x.NumberOfWomenToAssign);

				var preciseNumberPerTeam = (decimal)playersRemaining / (_teams.Count-i);

				var forThisTeam = (int) Math.Ceiling(preciseNumberPerTeam);

				if (gender == Gender.Male) {
					_teams[i].NumberOfMenToAssign = forThisTeam;
				}
				else {
					_teams[i].NumberOfWomenToAssign = forThisTeam;
				}
			}
		}
	}
}
