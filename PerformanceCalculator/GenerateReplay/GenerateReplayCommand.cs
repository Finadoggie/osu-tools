// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using McMaster.Extensions.CommandLineUtils;
using Newtonsoft.Json;
using osu.Framework.Extensions;
using osu.Game.Online.API;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Osu.Difficulty;
using osuTK;

namespace PerformanceCalculator.GenerateReplay
{
    [Command(Name = "generate_replay", Description = "Generates a replay for a map.")]
    public class GenerateReplayCommand : ProcessorCommand
    {
        [UsedImplicitly]
        [Argument(0, Name = "path", Description = "Required. A beatmap file (.osu), beatmap ID, or a folder containing .osu files to compute the difficulty for.")]
        public string Path { get; } = null!;

        [UsedImplicitly]
        [Argument(1, Name = "filename", Description = "The name of the outputted file.")]
        public string Filename { get; } = null!;

        public override void Execute()
        {
            const float x_min = -500f;
            const float x_max = 1000f;
            const float y_min = -500f;
            const float y_max = 1000f;

            var beatmap = ProcessorWorkingBeatmap.FromFileOrId(Path);

            // Get the ruleset
            var ruleset = LegacyHelper.GetRulesetFromLegacyID(0);
            OsuDifficultyCalculator calculator = (OsuDifficultyCalculator)ruleset.CreateDifficultyCalculator(beatmap);
            calculator.Calculate();

            var forces = calculator.GetForces(1.0);

            const string user = "Finadoggie";
            DateTime date = DateTime.UtcNow;

            List<ReplayCompressor.ReplayFrame> frames = new List<ReplayCompressor.ReplayFrame>();

            long prevLongTime = 0;

            foreach (var force in forces)
            {
                double currentRealTime = 0;
                long currentLongTime = 0;
                long deltaLongTime = 0;

                for (int i = 0; i < (int)force.ForceDuration - 1; i++)
                {
                    Vector2 pos = force.StartPosition;

                    float displacementTime = i;
                    Vector2 displacement = CalculateDisplacement((float)force.StartVelocity / force.PrevForce?.ScalingFactor ?? force.ScalingFactor, (float)force.StartVelocityAngle, (float)force.Acceleration / force.ScalingFactor, (float)force.AbsoluteAngle, displacementTime);

                    pos += displacement;

                    currentRealTime = force.StartTime + i;
                    currentLongTime = (long)currentRealTime;
                    deltaLongTime = currentLongTime - prevLongTime;
                    prevLongTime = currentLongTime;

                    // Console.WriteLine($"real: {currentRealTime}, long: {currentLongTime}, prev: {prevLongTime}, delta: {deltaLongTime}");

                    frames.Add(new ReplayCompressor.ReplayFrame
                    {
                        deltaTime = deltaLongTime,
                        x = Math.Max(x_min, Math.Min(x_max, pos.X)),
                        y = Math.Max(y_min, Math.Min(y_max, pos.Y)),
                        clicks = (i >= force.ForceDuration - 20 && force.EndsInClick) ? 0 : 5,
                    });
                }

                // Last frame is discretely written at end point
                currentRealTime = force.StartTime + force.ForceDuration;
                currentLongTime = (long)currentRealTime;
                deltaLongTime = currentLongTime - prevLongTime;
                prevLongTime = currentLongTime;

                // Console.WriteLine($"real: {currentRealTime}, long: {currentLongTime}, prev: {prevLongTime}, delta: {deltaLongTime}");

                frames.Add(new ReplayCompressor.ReplayFrame
                {
                    deltaTime = deltaLongTime,
                    x = Math.Max(x_min, Math.Min(x_max, force.EndPosition.X)),
                    y = Math.Max(y_min, Math.Min(y_max, force.EndPosition.Y)),
                    clicks = 5
                });
            }

            // Required as final dummy frame
            frames.Add(new ReplayCompressor.ReplayFrame { deltaTime = -12345, x = 0, y = 0, clicks = 0 });

            byte[] replayData = ReplayCompressor.CompressReplayFrames(frames.ToArray());

            var myReplay = new OsrGenerator.ReplayData
            {
                GameMode = 0, // osu! Standard
                GameVersion = 20231011,
                BeatmapHash = beatmap.BeatmapInfo.MD5Hash,
                PlayerName = user,
                ReplayHash = FormattableString.Invariant($"lazer-{user}-{date}").ComputeMD5Hash(),
                Count300 = 1337,
                Count100 = 0,
                Count50 = 0,
                CountGeki = 420,
                CountKatu = 0,
                Misses = 0,
                TotalScore = 5318008,
                MaxCombo = 69,
                IsPerfect = true,
                Mods = 4097, // NFSO
                LifeBarGraph = string.Empty,
                Timestamp = date.Ticks,
                ReplayFrameBytes = replayData.Length,
                ReplayFrameData = replayData,
                OnlineID = -1
            };

            OsrGenerator.WriteReplayHeader(Filename, myReplay);
        }

        public static Vector2 CalculateDisplacement(
            float vMag, float vAngle,
            float aMag, float aAngle,
            float time)
        {
            // 2. Break Velocity into components
            float vix = vMag * MathF.Cos(vAngle);
            float viy = vMag * MathF.Sin(vAngle);

            // 3. Break Acceleration into components
            float ax = aMag * MathF.Cos(aAngle);
            float ay = aMag * MathF.Sin(aAngle);

            // 4. Calculate displacement for each axis
            // dx = (vix * t) + (0.5 * ax * t^2)
            float dx = (vix * time) + (0.5f * ax * MathF.Pow(time, 2));
            float dy = (viy * time) + (0.5f * ay * MathF.Pow(time, 2));

            return new Vector2(dx, dy);
        }

        private class Result
        {
            [JsonProperty("ruleset_id")]
            public required int RulesetId { get; set; }

            [JsonProperty("beatmap_id")]
            public required int BeatmapId { get; set; }

            [JsonProperty("beatmap")]
            public required string Beatmap { get; set; }

            [JsonProperty("mods")]
            public required List<APIMod> Mods { get; set; }

            [JsonProperty("attributes")]
            public required DifficultyAttributes Attributes { get; set; }
        }
    }
}
