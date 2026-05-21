using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Text.Json;

namespace AlkoLog;

public partial class ProfilePageViewModel : ObservableObject
{
	[ObservableProperty]
	private string nameText;

	[ObservableProperty]
	private string ageText;

	[ObservableProperty]
	private string weightText;

	[ObservableProperty]
	private string gender;

	[ObservableProperty]
	private string photoPath;

	public string[] GenderOptions { get; } = ["Férfi", "Nő"];

	string filePath = Path.Combine(FileSystem.Current.AppDataDirectory, "profile.json");

	public ProfilePageViewModel()
	{
		NameText = string.Empty;
		AgeText = string.Empty;
		WeightText = string.Empty;
		Gender = string.Empty;
		// Alapból a beépített profilkép látszik, amíg a felhasználó nem készít saját fotót.
		PhotoPath = "profilkep.png";
	}

	[RelayCommand]
	async Task TakePhotoAsync()
	{
		try
		{
			if (!await PermissionHelper.EnsureCameraPermissionAsync())
			{
				return;
			}

			var photo = await MediaPicker.Default.CapturePhotoAsync();

			if (photo != null)
			{
				string targetPath = Path.Combine(FileSystem.Current.AppDataDirectory, "profilephoto.jpg");

				await using var sourceStream = await photo.OpenReadAsync();
				await using var targetStream = File.Open(targetPath, FileMode.Create, FileAccess.Write);
				await sourceStream.CopyToAsync(targetStream);

				PhotoPath = targetPath;
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine("HIBA KÉPKÉSZÍTÉSKOR: " + ex.Message);
		}
	}

	public void ResetData()
	{
		NameText = string.Empty;
		AgeText = string.Empty;
		WeightText = string.Empty;
		Gender = string.Empty;
		PhotoPath = "profilkep.png";
	}

	[RelayCommand]
	async Task SaveAsync()
	{
		await SaveDataAsync();

		// Mentés után visszaküldjük a felhasználót a főoldalra, ahogy a mintában is megszokott.
		await Shell.Current.GoToAsync("//MainPage");
	}

	public async Task SaveDataAsync()
	{
		try
		{
			int.TryParse(AgeText, out int age);
			double.TryParse(WeightText, out double weight);

			var profile = new UserProfile
			{
				Name = NameText ?? string.Empty,
				Age = age,
				Weight = weight,
				Gender = Gender ?? string.Empty,
				PhotoPath = PhotoPath ?? string.Empty
			};

			string jsonText = JsonSerializer.Serialize(profile);
			await File.WriteAllTextAsync(filePath, jsonText);
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine("HIBA MENTÉSKOR: " + ex.Message);
		}
	}

	public async Task LoadDataAsync()
	{
		try
		{
			if (File.Exists(filePath))
			{
				string jsonText = await File.ReadAllTextAsync(filePath);
				var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
				var profile = JsonSerializer.Deserialize<UserProfile>(jsonText, options);

				if (profile != null)
				{
					NameText = profile.Name;
					AgeText = profile.Age.ToString();
					WeightText = profile.Weight.ToString();
					Gender = profile.Gender;
					PhotoPath = string.IsNullOrWhiteSpace(profile.PhotoPath) ? "profilkep.png" : profile.PhotoPath;
				}
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine("HIBA BETÖLTÉSKOR: " + ex.Message);
		}
	}
}