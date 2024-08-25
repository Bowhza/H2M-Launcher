using System.Text.Json;

using H2MLauncher.Core.Models;
using H2MLauncher.UI;

namespace H2MLauncher.Core.Utilities
{
    public static class UserFavoritesUtility
    {
        // Will seek for ./Storage/UserFavorites.json
        private static readonly string FilePath = Path.Combine(Constants.LocalDir, "Storage", "UserFavorites.json");

        // Method to get the user's favorites from the JSON file.
        public static List<UserFavorite> GetFavorites()
        {
            if (!File.Exists(FilePath))
                return new List<UserFavorite>();
            

            string json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<List<UserFavorite>>(json) ?? new List<UserFavorite>();
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
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(favorites, options);
            File.WriteAllText(FilePath, json);
        }
    }
}
