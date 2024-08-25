using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

using H2MLauncher.Core.Models;

using Newtonsoft.Json;

namespace H2MLauncher.Core.Utilities
{
    public static class UserFavoritesUtility
    {
        // Get the project directory (two levels up from the bin/Debug directory)
        private static readonly string FilePath = Path.Combine(Directory.GetParent(Directory.GetCurrentDirectory()).Parent.Parent.FullName, "Storage", "UserFavorites.json");

        // Method to get the user's favorites from the JSON file.
        public static List<UserFavorite> GetFavorites()
        {
            if (!File.Exists(FilePath))
            {
                // If the file doesn't exist, return an empty list.
                return new List<UserFavorite>();
            }

            string json = File.ReadAllText(FilePath);
            return JsonConvert.DeserializeObject<List<UserFavorite>>(json) ?? new List<UserFavorite>();
        }

        // Method to add a favorite to the JSON file.
        public static void AddFavorite(UserFavorite favorite)
        {
            var favorites = GetFavorites();

            // Add the new favorite to the list.
            favorites.Add(favorite);

            // Save the updated list to the JSON file.
            SaveFavorites(favorites);
        }

        // Method to remove a favorite from the JSON file.
        public static void RemoveFavorite(string serverIp)
        {
            var favorites = GetFavorites();

            // Remove the favorite that matches the provided ServerIp.
            favorites.RemoveAll(fav => fav.ServerIp == serverIp);

            // Save the updated list to the JSON file.
            SaveFavorites(favorites);
        }

        // Private method to save the list of favorites to the JSON file.
        private static void SaveFavorites(List<UserFavorite> favorites)
        {
            string json = JsonConvert.SerializeObject(favorites, Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText(FilePath, json);
        }
    }
}
