using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace AlkoLog;

public partial class MainPageViewModel : ObservableObject
{
	const double BacEliminationRatePerHour = 0.15;

	public ObservableCollection<ConsumptionRecord> ConsumptionList { get; private set; }

	[ObservableProperty]
	private ConsumptionRecord selectedItem;

	[ObservableProperty]
	private string profileName = "Profil nélkül";

	[ObservableProperty]
	private double currentBac;

	[ObservableProperty]
	private string bacText = "0.000 %";

	[ObservableProperty]
	private string soberTimeText = "Várható teljes kijózanodás: -";

	[ObservableProperty]
	private Color mainBackgroundColor = Colors.DimGray;

	[ObservableProperty]
	private Color mainTextColor = Colors.WhiteSmoke;

	[ObservableProperty]
	private bool isBackgroundColorEnabled = true;

	[ObservableProperty]
	private string backgroundColorToggleText = "Háttérszín kikapcsolása";

	string consumptionFilePath = Path.Combine(FileSystem.Current.AppDataDirectory, "consumed.json");
	string profileFilePath = Path.Combine(FileSystem.Current.AppDataDirectory, "profile.json");
	string catalogFilePath = Path.Combine(FileSystem.Current.AppDataDirectory, "catalog.json");

	UserProfile? currentProfile;

	public MainPageViewModel()
	{
		// A főoldal egy figyelhető listát használ, hogy a képernyő magától frissüljön.
		ConsumptionList = new ObservableCollection<ConsumptionRecord>();
	}

	public void AddConsumption(ConsumptionRecord record)
	{
		if (record == null)
		{
			return;
		}

		ConsumptionList.Add(record);
		RecalculateSummary();
	}

	[RelayCommand]
	void ToggleBackgroundColor()
	{
		IsBackgroundColorEnabled = !IsBackgroundColorEnabled;
		BackgroundColorToggleText = IsBackgroundColorEnabled ? "Háttérszín kikapcsolása" : "Háttérszín bekapcsolása";
		RecalculateSummary();
	}

	public void ResetData()
	{
		ConsumptionList.Clear();
		SelectedItem = null;
		ProfileName = "Profil nélkül";
		CurrentBac = 0;
		BacText = "0.000 %";
		SoberTimeText = "Várható teljes kijózanodás: -";
		MainBackgroundColor = GetDefaultPageBackgroundColor();
		MainTextColor = GetReadableTextColor(MainBackgroundColor);
		IsBackgroundColorEnabled = true;
		BackgroundColorToggleText = "Háttérszín kikapcsolása";
		currentProfile = null;
	}

	[RelayCommand]
	void DeleteSelected()
	{
		if (SelectedItem != null)
		{
			ConsumptionList.Remove(SelectedItem);
			SelectedItem = null;
			RecalculateSummary();
		}
	}

	[RelayCommand]
	async Task ShareSelectedAsync()
	{
		string shareText = await BuildShareTextAsync();

		await Share.Default.RequestAsync(new ShareTextRequest
		{
			Title = "AlkoLog megosztás",
			Text = shareText
		});
	}

	[RelayCommand]
	async Task RecordDrinkAsync()
	{
		try
		{
			var catalogItems = await LoadCatalogItemsAsync();

			if (catalogItems.Count == 0)
			{
				await Shell.Current.DisplayAlert("Figyelem", "Nincs elérhető ital a katalógusban.", "OK");
				return;
			}

			string chosenDrinkName = await Shell.Current.DisplayActionSheet("Válassz italt", "Mégse", null, catalogItems.Select(item => item.Name).ToArray());

			if (string.IsNullOrWhiteSpace(chosenDrinkName) || chosenDrinkName == "Mégse")
			{
				return;
			}

			var selectedDrink = catalogItems.FirstOrDefault(item => item.Name == chosenDrinkName);

			if (selectedDrink == null)
			{
				return;
			}

			string amountText = await Shell.Current.DisplayPromptAsync(
				"Ital rögzítése",
				$"Mennyi millilitert ittál meg a(z) {selectedDrink.Name} italból?",
				"Rögzítés",
				"Mégse",
				"50",
				keyboard: Keyboard.Numeric);

			if (string.IsNullOrWhiteSpace(amountText))
			{
				return;
			}

			double.TryParse(amountText, out double amountMl);
			var currentLocation = await GetCurrentLocationAsync();

			var record = new ConsumptionRecord
			{
				DrinkName = selectedDrink.Name,
				AlcoholPercent = selectedDrink.AlcoholPercent,
				AmountMl = amountMl,
				Timestamp = DateTime.Now,
				Latitude = currentLocation.Latitude,
				Longitude = currentLocation.Longitude
			};

			AddConsumption(record);
			await SaveDataAsync();
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine("HIBA ITAL RÖGZÍTÉSKOR A FŐOLDALON: " + ex.Message);
		}
	}

	[RelayCommand]
	async Task RepeatLastDrinkAsync()
	{
		var lastRecord = ConsumptionList.LastOrDefault();

		if (lastRecord == null)
		{
			await Shell.Current.DisplayAlert("Figyelem", "Nincs korábbi ital, amit meg lehetne ismételni.", "OK");
			return;
		}

		try
		{
			var currentLocation = await GetCurrentLocationAsync();

			var repeatedRecord = new ConsumptionRecord
			{
				DrinkName = lastRecord.DrinkName,
				AlcoholPercent = lastRecord.AlcoholPercent,
				AmountMl = lastRecord.AmountMl,
				Timestamp = DateTime.Now,
				Latitude = currentLocation.Latitude,
				Longitude = currentLocation.Longitude
			};

			AddConsumption(repeatedRecord);
			await SaveDataAsync();
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine("HIBA UTOLSÓ ITAL ISMÉTLÉSEKOR: " + ex.Message);
		}
	}

	public async Task LoadDataAsync()
	{
		try
		{
			if (File.Exists(consumptionFilePath) && ConsumptionList.Count == 0)
			{
				string jsonText = await File.ReadAllTextAsync(consumptionFilePath);
				var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
				var items = JsonSerializer.Deserialize<List<ConsumptionRecord>>(jsonText, options);

				if (items != null)
				{
					foreach (var item in items)
					{
						ConsumptionList.Add(item);
					}
				}
			}

			await LoadProfileAsync();
			RecalculateSummary();
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine("HIBA FŐOLDAL BETÖLTÉSKOR: " + ex.Message);
		}
	}

	public async Task SaveDataAsync()
	{
		try
		{
			string jsonText = JsonSerializer.Serialize(ConsumptionList);
			await File.WriteAllTextAsync(consumptionFilePath, jsonText);
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine("HIBA FŐOLDAL MENTÉSKOR: " + ex.Message);
		}
	}

	async Task LoadProfileAsync()
	{
		try
		{
			if (File.Exists(profileFilePath))
			{
				string jsonText = await File.ReadAllTextAsync(profileFilePath);
				var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
				currentProfile = JsonSerializer.Deserialize<UserProfile>(jsonText, options);

				if (currentProfile != null && !string.IsNullOrWhiteSpace(currentProfile.Name))
				{
					ProfileName = currentProfile.Name;
				}
			}
			else
			{
				currentProfile = null;
				ProfileName = "Profil nélkül";
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine("HIBA PROFIL BETÖLTÉSKOR: " + ex.Message);
		}
	}

	async Task<List<DrinkCatalogItem>> LoadCatalogItemsAsync()
	{
		try
		{
			if (File.Exists(catalogFilePath))
			{
				string jsonText = await File.ReadAllTextAsync(catalogFilePath);
				var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
				var items = JsonSerializer.Deserialize<List<DrinkCatalogItem>>(jsonText, options);

				if (items != null && items.Count > 0)
				{
					return items;
				}
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine("HIBA KATALÓGUS OLVASÁSKOR A FŐOLDALON: " + ex.Message);
		}

		return new List<DrinkCatalogItem>
		{
			new DrinkCatalogItem { Name = "Világos sör", AlcoholPercent = 5.0, ImagePath = "ital.png" },
			new DrinkCatalogItem { Name = "Vörösbor", AlcoholPercent = 12.5, ImagePath = "ital.png" },
			new DrinkCatalogItem { Name = "Házi pálinka", AlcoholPercent = 50.0, ImagePath = "ital.png" }
		};
	}

	async Task<(double Latitude, double Longitude)> GetCurrentLocationAsync()
	{
		double latitude = 0;
		double longitude = 0;

		try
		{
			if (await PermissionHelper.EnsureLocationPermissionAsync())
			{
				var location = await Geolocation.Default.GetLastKnownLocationAsync();

				if (location == null)
				{
					location = await Geolocation.Default.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.Medium));
				}

				if (location != null)
				{
					latitude = location.Latitude;
					longitude = location.Longitude;
				}
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine("HIBA GPS LEKÉRÉSKOR: " + ex.Message);
		}

		return (latitude, longitude);
	}

	void RecalculateSummary()
	{
		DateTime now = DateTime.Now;

		if (!IsBackgroundColorEnabled)
		{
			MainBackgroundColor = GetDefaultPageBackgroundColor();
			MainTextColor = GetReadableTextColor(MainBackgroundColor);
		}

		if (currentProfile == null || currentProfile.Weight <= 0)
		{
			CurrentBac = 0;
			BacText = "0.000 %";
			SoberTimeText = "Várható teljes kijózanodás: -";
			if (IsBackgroundColorEnabled)
			{
				MainBackgroundColor = Colors.LightGreen;
				MainTextColor = GetReadableTextColor(MainBackgroundColor);
			}
			else
			{
				MainBackgroundColor = GetDefaultPageBackgroundColor();
				MainTextColor = GetReadableTextColor(MainBackgroundColor);
			}
			return;
		}

		double genderFactor = currentProfile.Gender == "Nő" ? 0.6 : 0.7;
		double currentBacFromRecords = ConsumptionList.Sum(item => GetCurrentBacContribution(item, now, currentProfile.Weight, genderFactor));

		CurrentBac = Math.Max(0, currentBacFromRecords);
		BacText = $"{CurrentBac:0.000} %";
		if (IsBackgroundColorEnabled)
		{
			MainBackgroundColor = GetBackgroundColor(CurrentBac);
			MainTextColor = GetReadableTextColor(MainBackgroundColor);
		}

		if (CurrentBac <= 0)
		{
			SoberTimeText = "Várható teljes kijózanodás: -";
			if (IsBackgroundColorEnabled)
			{
				MainBackgroundColor = Colors.LightGreen;
				MainTextColor = GetReadableTextColor(MainBackgroundColor);
			}
			else
			{
				MainBackgroundColor = GetDefaultPageBackgroundColor();
				MainTextColor = GetReadableTextColor(MainBackgroundColor);
			}
			return;
		}

		double soberHours = CurrentBac / BacEliminationRatePerHour;
		DateTime soberTime = now.AddHours(soberHours);
		SoberTimeText = $"Várható teljes kijózanodás: {BuildSoberTimeText(soberTime)}";
	}

	double GetCurrentBacContribution(ConsumptionRecord item, DateTime referenceTime, double weight, double genderFactor)
	{
		double initialBac = (item.AmountMl * (item.AlcoholPercent / 100.0) * 0.789) / (weight * genderFactor);
		double elapsedHours = Math.Max(0, (referenceTime - item.Timestamp).TotalHours);

		return Math.Max(0, initialBac - (elapsedHours * BacEliminationRatePerHour));
	}

	Color GetBackgroundColor(double bac)
	{
		double clampedBac = Math.Clamp(bac, 0, 2.0);

		if (clampedBac <= 0.5)
		{
			return InterpolateColor(Colors.LightGreen, Colors.Gold, clampedBac / 0.5);
		}

		if (clampedBac <= 1.5)
		{
			return InterpolateColor(Colors.Gold, Colors.IndianRed, (clampedBac - 0.5) / 1.0);
		}

		return InterpolateColor(Colors.IndianRed, Colors.DarkRed, (clampedBac - 1.5) / 0.5);
	}

	Color InterpolateColor(Color startColor, Color endColor, double amount)
	{
		double clampedAmount = Math.Clamp(amount, 0, 1);

		double red = startColor.Red + ((endColor.Red - startColor.Red) * clampedAmount);
		double green = startColor.Green + ((endColor.Green - startColor.Green) * clampedAmount);
		double blue = startColor.Blue + ((endColor.Blue - startColor.Blue) * clampedAmount);

		return Color.FromRgb(red, green, blue);
	}

	Color GetDefaultPageBackgroundColor()
	{
		return Colors.DimGray;
	}

	string BuildSoberTimeText(DateTime soberTime)
	{
		DateTime today = DateTime.Today;
		DateTime tomorrow = today.AddDays(1);

		if (soberTime.Date == today)
		{
			return $"Ma {soberTime:HH:mm}-kor";
		}

		if (soberTime.Date == tomorrow)
		{
			return $"Holnap {soberTime:HH:mm}-kor";
		}

		return $"{soberTime:yyyy.MM.dd HH:mm}-kor";
	}

	Color GetReadableTextColor(Color backgroundColor)
	{
		double brightness = (backgroundColor.Red * 0.299) + (backgroundColor.Green * 0.587) + (backgroundColor.Blue * 0.114);
		return brightness < 0.5 ? Colors.WhiteSmoke : Colors.Black;
	}

	public void RefreshSummary()
	{
		RecalculateSummary();
	}

	async Task<string> BuildShareTextAsync()
	{
		string shareText;

		if (CurrentBac <= 0)
		{
			shareText = "AlkoLog jelentés: Teljesen tiszta vagyok (0,000 %)! Hivatalosan is biztonságosan vezethetek.";
		}
		else
		{
			shareText = $"Az aktuális véralkoholszintem az AlkoLog szerint: {CurrentBac:0.000} %. {SoberTimeText}";
		}

		string positionText = await BuildCurrentPositionTextAsync();
		return $"{shareText}\n{positionText}";
	}

	async Task<string> BuildCurrentPositionTextAsync()
	{
		try
		{
			if (await PermissionHelper.EnsureLocationPermissionAsync())
			{
				var location = await Geolocation.Default.GetLastKnownLocationAsync();

				if (location == null)
				{
					location = await Geolocation.Default.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.Medium));
				}

				if (location != null)
				{
					return $"Aktuális pozíció: {location.Latitude:0.#####}, {location.Longitude:0.#####}";
				}
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine("HIBA HELYADATAK MEGOSZTÁSÁNÁL: " + ex.Message);
		}

		return "Aktuális pozíció: nem elérhető";
	}
}