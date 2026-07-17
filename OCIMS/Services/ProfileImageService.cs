using System;
using System.IO;
using Microsoft.Win32;

namespace eSureHi.Services
{
    public static class ProfileImageService
    {
        private static string ProfileImageDirectory =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "eSureHi",
                "ProfileImages");

        public static string? GetCurrentProfileImagePath()
        {
            var user = AuthService.Instance.CurrentUser;
            if (user is null) return null;

            var userImage = GetUserImagePath(user.UserId);
            if (File.Exists(userImage))
                return userImage;

            var employeePhoto = AuthService.Instance.CurrentEmployee?.PhotoPath;
            return !string.IsNullOrWhiteSpace(employeePhoto) && File.Exists(employeePhoto)
                ? employeePhoto
                : null;
        }

        public static string? PickAndSaveCurrentProfileImage()
        {
            var user = AuthService.Instance.CurrentUser;
            if (user is null) return null;

            var dialog = new OpenFileDialog
            {
                Title = "Select Profile Image",
                Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp|All Files|*.*"
            };

            if (dialog.ShowDialog() != true)
                return null;

            Directory.CreateDirectory(ProfileImageDirectory);
            var extension = Path.GetExtension(dialog.FileName);
            if (string.IsNullOrWhiteSpace(extension))
                extension = ".png";

            var target = Path.Combine(ProfileImageDirectory, $"user_{user.UserId}{extension.ToLowerInvariant()}");
            File.Copy(dialog.FileName, target, true);

            if (AuthService.Instance.CurrentEmployee is not null)
                AuthService.Instance.CurrentEmployee.PhotoPath = target;

            return target;
        }

        private static string GetUserImagePath(int userId)
        {
            Directory.CreateDirectory(ProfileImageDirectory);

            foreach (var extension in new[] { ".png", ".jpg", ".jpeg", ".bmp" })
            {
                var path = Path.Combine(ProfileImageDirectory, $"user_{userId}{extension}");
                if (File.Exists(path))
                    return path;
            }

            return Path.Combine(ProfileImageDirectory, $"user_{userId}.png");
        }
    }
}
