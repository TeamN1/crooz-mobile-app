﻿using Crooz;
using Microsoft.ProjectOxford.Emotion;
using Microsoft.ProjectOxford.Emotion.Contract;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SharedProject
{
    class Core
    {
        private static async Task<Emotion[]> GetEmotions(Stream stream)
        {
            string emotionKey = "ac6a1ec8050f47faa18500c365bf3efa";

            EmotionServiceClient emotionClient = new EmotionServiceClient(emotionKey);

            var emotionResults = await emotionClient.RecognizeAsync(stream);

            if (emotionResults == null || emotionResults.Count() == 0)
            {
                throw new Exception("Can't detect face");
            }

            return emotionResults;
        }
        //Average happiness calculation in case of multiple people
        public static async Task<float> GetAverageHappinessScore(Stream stream)
        {
            Emotion[] emotionResults = await GetEmotions(stream);

            float score = 0;
            float angerScore = 0;

            foreach (var emotionResult in emotionResults)
            {
                score = score + emotionResult.Scores.Happiness;
                angerScore = angerScore + emotionResult.Scores.Anger;
                Console.WriteLine();
            }

            return score / emotionResults.Count();
        }

        public static async Task<Mood> GetMood(Stream stream)
        {
            Emotion[] emotionResults = await GetEmotions(stream);

            // Get first emotion

            return new Mood
            {
                surprise = emotionResults[0].Scores.Surprise,
                happiness = emotionResults[0].Scores.Happiness,
                neutral = emotionResults[0].Scores.Neutral,
                sadness = emotionResults[0].Scores.Sadness,
                anger = emotionResults[0].Scores.Anger
            };
        }

        public static string GetHappinessMessage(float score)
        {
            score = score * 100;
            double result = Math.Round(score, 2);

            if (score >= 50)
                return result + " % Mood: Happy";
            else
                return result + "% Mood: Sad";
        }
    }
}
