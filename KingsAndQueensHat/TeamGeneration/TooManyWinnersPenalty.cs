﻿using System;
using System.Linq;
using KingsAndQueensHat.Model;

namespace KingsAndQueensHat.TeamGeneration
{
    public class TooManyWinnersPenalty : IPenalty
    {
        private readonly IPlayerProvider _players;

        public TooManyWinnersPenalty(IPlayerProvider players)
        {
            _players = players;
        }

        public double ScorePenalty(TeamSet teamSet)
        {
            return ScorePenaltyForGender(teamSet, Gender.Male)
                 + ScorePenaltyForGender(teamSet, Gender.Female);
        }

        public double ScorePenaltyForGender(TeamSet teamSet, Gender gender)
        {
            var maxScore = _players.MaxGameScore(gender);

            var winningPerTeam = teamSet.Teams.Select(t => t.Players.Count(p => p.GameScore == maxScore)).ToList();

            var totalWinning = winningPerTeam.Sum();
            var expectedWinnersPerTeam = totalWinning / (double)teamSet.TeamCount;

            // Sum the deviations from the expected team skill
            var result = winningPerTeam.Sum(s => Math.Abs(s - expectedWinnersPerTeam));
            return result;
        }

        /// <summary>
        /// Having too many winners is less important than other factors
        /// </summary>
        public double Weighting { get { return 0.5; } }
    }
}