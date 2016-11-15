using Crooz;
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

        public static async Task<Emotion> GetEmotion(Stream stream)
        {
            Emotion[] emotionResults = await GetEmotions(stream);

            // Get first emotion

            return emotionResults.First();
        }

        public static Mood GetMood(Emotion emotion)
        {
            // Get first emotion

            return new Mood
            {
                surprise = emotion.Scores.Surprise,
                happiness = emotion.Scores.Happiness,
                neutral = emotion.Scores.Neutral,
                sadness = emotion.Scores.Sadness,
                anger = emotion.Scores.Anger
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
