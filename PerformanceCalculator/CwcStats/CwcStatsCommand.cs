// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using McMaster.Extensions.CommandLineUtils;
using CsvHelper;
using CsvHelper.Configuration.Attributes;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Scoring.Legacy;

namespace PerformanceCalculator.CwcStats
{
    [HelpOption("-?|-h|--help")]
    [Command(Name = "cwcstats", Description = "Calculates pp from shdews's owc stats .csv file")]
    public class CwcStatsCommand : ProcessorCommand
    {
        [UsedImplicitly]
        [Required]
        [Argument(0, Name = "input filename", Description = ".csv file containing all of the stats")]
        public string InputFilename { get; }

        [UsedImplicitly]
        [Required]
        [Argument(1, Name = "output filename", Description = "name of .tsv file containing pp values for each score")]
        public string OutputFilename { get; }

        public override void Execute()
        {
            List<OutputEntry> outputEntries = new List<OutputEntry>();

            // Parse .csv
            using (var reader = new StreamReader(InputFilename))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                var inputEntries = csv.GetRecords<InputEntry>();

                // Calculate each beatmap
                foreach (var entry in inputEntries.ToList())
                    outputEntries.Add(calculateEntry(entry));
            }

            // Output to new csv
            using (var writer = new StreamWriter(OutputFilename))
            {
                writer.WriteLine("ScoreId\tPP");

                foreach (var entry in outputEntries)
                {
                    writer.Write(entry.ScoreId);
                    writer.Write("\t");
                    writer.Write(entry.PP);
                    writer.Write("\n");
                }
            }
        }

        private OutputEntry calculateEntry(InputEntry entry)
        {
            var ruleset = LegacyHelper.GetRulesetFromLegacyID(2);

            var workingBeatmap = ProcessorWorkingBeatmap.FromFileOrId(entry.BeatmapId.ToString());
            var mods = ParseMods(ruleset, processModsString(entry.Mods), Array.Empty<string>());
            var beatmap = workingBeatmap.GetPlayableBeatmap(ruleset.RulesetInfo, mods);

            int beatmapMaxCombo = beatmap.GetMaxCombo();
            // var statistics = CatchSimulateCommand.GenerateHitResults(beatmap, entry.Accuracy, entry.CountMiss, entry.Count50, entry.Count100);

            var statistics = new Dictionary<HitResult, int>
            {
                { HitResult.Great, entry.Count300 },
                { HitResult.LargeTickHit, entry.Count100 },
                { HitResult.SmallTickHit, entry.Count50 },
                { HitResult.SmallTickMiss, entry.CountKatu },
                { HitResult.Miss, entry.CountMiss }
            };
            var scoreInfo = new ScoreInfo(beatmap.BeatmapInfo, ruleset.RulesetInfo)
            {
                Accuracy = entry.Accuracy,
                MaxCombo = entry.MaxCombo,
                Statistics = statistics,
                Mods = mods
            };

            if (scoreInfo.GetCountKatu() != entry.CountKatu)
            {
                Console.WriteLine($"{entry.ScoreId}");
                Console.WriteLine($"num300: {scoreInfo.GetCount300()} {entry.Count300}");
                Console.WriteLine($"num100: {scoreInfo.GetCount100()} {entry.Count100}");
                Console.WriteLine($"num50: {scoreInfo.GetCount50()} {entry.Count50}");
                Console.WriteLine($"numKatu: {scoreInfo.GetCountKatu()} {entry.CountKatu}");
                Console.WriteLine($"numMiss: {scoreInfo.GetCountMiss()} {entry.CountMiss}");
            }

            var difficultyCalculator = ruleset.CreateDifficultyCalculator(workingBeatmap);
            var difficultyAttributes = difficultyCalculator.Calculate(mods);
            var performanceCalculator = ruleset.CreatePerformanceCalculator();
            var performanceAttributes = performanceCalculator?.Calculate(scoreInfo, difficultyAttributes);

            Debug.Assert(performanceAttributes != null, nameof(performanceAttributes) + " != null");

            return new OutputEntry()
            {
                ScoreId = entry.ScoreId,
                PP = performanceAttributes.Total
            };
        }

        private string[] processModsString(string modsString)
        {
            List<string> mods = new List<string>();

            // mods.Add("NF");
            mods.Add("CL");
            mods.Add("NF");
            mods.Add("SV2");

            if (modsString == "NM") return mods.ToArray();

            if (modsString.Contains("HD")) mods.Add("HD");
            if (modsString.Contains("HR")) mods.Add("HR");
            if (modsString.Contains("DT")) mods.Add("DT");
            if (modsString.Contains("FL")) mods.Add("FL");
            if (modsString.Contains("EZ")) mods.Add("EZ");
            if (modsString.Contains("HT")) mods.Add("HT");
            if (modsString.Contains("SO")) mods.Add("SO");

            return mods.ToArray();
        }

        private class InputEntry
        {
            [Name("match_name")]
            public string MatchName { get; set; }

            [Name("match_id")]
            public int MatchId { get; set; }

            [Name("start_time")]
            public string StartTime { get; set; }

            [Name("end_time")]
            public string EndTime { get; set; }

            [Name("score_id")]
            public string ScoreId { get; set; }

            [Name("beatmap_id")]
            public int BeatmapId { get; set; }

            [Name("user_id")]
            public int UserId { get; set; }

            [Name("score")]
            public int Score { get; set; }

            [Name("accuracy")]
            public double Accuracy { get; set; }

            [Name("max_combo")]
            public int MaxCombo { get; set; }

            [Name("count_geki")]
            public int CountGeki { get; set; }

            [Name("count_300")]
            public int Count300 { get; set; }

            [Name("count_katu")]
            public int CountKatu { get; set; }

            [Name("count_100")]
            public int Count100 { get; set; }

            [Name("count_50")]
            public int Count50 { get; set; }

            [Name("count_miss")]
            public int CountMiss { get; set; }

            [Name("mods (no NF)")]
            public string Mods { get; set; }

            [Name("grade")]
            public string Grade { get; set; }
        }

        private class OutputEntry
        {
            public string ScoreId { get; set; }
            public double PP { get; set; }
        }
    }
}
