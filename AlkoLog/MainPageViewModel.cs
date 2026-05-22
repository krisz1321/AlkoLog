using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
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
	private string repeatLastDrinkButtonText = string.Empty;

	[ObservableProperty]
	private bool isRepeatLastDrinkButtonVisible;

	string consumptionFilePath = Path.Combine(FileSystem.Current.AppDataDirectory, "consumed.json");
	string profileFilePath = Path.Combine(FileSystem.Current.AppDataDirectory, "profile.json");
	string catalogFilePath = Path.Combine(FileSystem.Current.AppDataDirectory, "catalog.json");

	readonly HistoryPageViewModel _historyPageViewModel;

	UserProfile? currentProfile;

	public MainPageViewModel(HistoryPageViewModel historyPageViewModel)
	{
		// A főoldal egy figyelhető listát használ, hogy a képernyő magától frissüljön.
		_historyPageViewModel = historyPageViewModel;
		ConsumptionList = new ObservableCollection<ConsumptionRecord>();
		ConsumptionList.CollectionChanged += ConsumptionList_CollectionChanged;
		UpdateRepeatLastDrinkState();
	}

	void ConsumptionList_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		UpdateRepeatLastDrinkState();
	}

	public void AddConsumption(ConsumptionRecord record)
	{
		if (record == null)
		{
			return;
		}

		ConsumptionList.Add(record);
		RecalculateSummary();
		TryVibrate();
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
		currentProfile = null;
	}

	void UpdateRepeatLastDrinkState()
	{
		var lastRecord = ConsumptionList.LastOrDefault();

		if (lastRecord == null || string.IsNullOrWhiteSpace(lastRecord.DrinkName))
		{
			RepeatLastDrinkButtonText = string.Empty;
			IsRepeatLastDrinkButtonVisible = false;
			return;
		}

		RepeatLastDrinkButtonText = $"{lastRecord.DrinkName} - {lastRecord.AmountMl:0.#} ml";
		IsRepeatLastDrinkButtonVisible = true;
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
	async Task OpenHistoryAsync()
	{
		await MoveEmptiedRecordsToHistoryAsync(forceMoveAllEmpty: true);
		await Shell.Current.GoToAsync("//HistoryPage");
	}

	[RelayCommand]
	void ToggleSelected(ConsumptionRecord record)
	{
		if (record == null)
		{
			return;
		}

		if (ReferenceEquals(SelectedItem, record))
		{
			record.IsSelected = false;
			SelectedItem = null;
			return;
		}

		if (SelectedItem != null)
		{
			SelectedItem.IsSelected = false;
		}

		SelectedItem = record;
		record.IsSelected = true;
	}

	[RelayCommand]
	async Task MoveToHistoryAsync(ConsumptionRecord record)
	{
		if (record == null || !record.IsEmpty || !ConsumptionList.Contains(record))
		{
			return;
		}

		ConsumptionList.Remove(record);
		_historyPageViewModel.AddRecord(record);
		await SaveDataAsync();
		await _historyPageViewModel.SaveDataAsync();
		await RefreshSummaryAsync(true, false);
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

			string defaultAmountText = GetDefaultAmount(selectedDrink).ToString("0.#");

			string amountText = await Shell.Current.DisplayPromptAsync(
				"Ital rögzítése",
				$"Mennyi millilitert ittál meg a(z) {selectedDrink.Name} italból?",
				"Rögzítés",
				"Mégse",
				defaultAmountText,
				keyboard: Keyboard.Numeric);

			if (string.IsNullOrWhiteSpace(amountText))
			{
				amountText = defaultAmountText;
			}

			if (!double.TryParse(amountText, out double amountMl))
			{
				amountMl = GetDefaultAmount(selectedDrink);
			}
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
			await RefreshSummaryAsync(true, false);
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

	public async Task RefreshSummaryAsync(bool updateConsumptionVisuals = true, bool allowVibration = true)
	{
		await MoveEmptiedRecordsToHistoryAsync(forceMoveAllEmpty: false);
		RecalculateSummary(updateConsumptionVisuals, allowVibration);
	}

	async Task MoveEmptiedRecordsToHistoryAsync(bool forceMoveAllEmpty)
	{
		if (currentProfile == null || currentProfile.Weight <= 0)
		{
			return;
		}

		double genderFactor = currentProfile.Gender == "Nő" ? 0.6 : 0.7;
		DateTime now = DateTime.Now;

		var recordsToMove = ConsumptionList
			.Where(item => ShouldMoveToHistory(item, now, currentProfile.Weight, genderFactor, forceMoveAllEmpty))
			.OrderBy(item => item.Timestamp)
			.ToList();

		if (recordsToMove.Count == 0)
		{
			return;
		}

		foreach (var record in recordsToMove)
		{
			ConsumptionList.Remove(record);
		}

		_historyPageViewModel.AddRecords(recordsToMove);
		await SaveDataAsync();
		await _historyPageViewModel.SaveDataAsync();
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

	bool ShouldMoveToHistory(ConsumptionRecord item, DateTime referenceTime, double weight, double genderFactor, bool forceMoveAllEmpty)
	{
		DateTime emptyAt = GetEmptyTime(item, weight, genderFactor);

		if (referenceTime < emptyAt)
		{
			return false;
		}

		if (forceMoveAllEmpty)
		{
			return true;
		}

		return referenceTime >= emptyAt.AddHours(24);
	}

	DateTime GetEmptyTime(ConsumptionRecord item, double weight, double genderFactor)
	{
		double totalEmptyHours = GetTotalEmptyHours(item, weight, genderFactor);
		return item.Timestamp.AddHours(totalEmptyHours);
	}

	double GetTotalEmptyHours(ConsumptionRecord item, double weight, double genderFactor)
	{
		double initialBac = (item.AmountMl * (item.AlcoholPercent / 100.0) * 0.789) / (weight * genderFactor);
		return Math.Max(0, initialBac) / BacEliminationRatePerHour;
	}

		double GetDefaultAmount(DrinkCatalogItem drink)
		{
			return drink.DefaultAmountMl > 0 ? drink.DefaultAmountMl : 50;
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
			new DrinkCatalogItem { Name = "Világos sör", AlcoholPercent = 5.0, DefaultAmountMl = 500, ImagePath = "ital.png" },
			new DrinkCatalogItem { Name = "Vörösbor", AlcoholPercent = 12.5, DefaultAmountMl = 150, ImagePath = "ital.png" },
			new DrinkCatalogItem { Name = "Házi pálinka", AlcoholPercent = 50.0, DefaultAmountMl = 50, ImagePath = "ital.png" }
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

	void RecalculateSummary(bool updateConsumptionVisuals = true, bool allowVibration = true)
	{
		DateTime now = DateTime.Now;
		if (updateConsumptionVisuals)
		{
			UpdateConsumptionVisualStates(now, allowVibration);
		}

		if (!(currentProfile?.BackgroundColorEnabled ?? true))
		{
			MainBackgroundColor = GetDefaultPageBackgroundColor();
			MainTextColor = GetReadableTextColor(MainBackgroundColor);
		}

		if (currentProfile == null || currentProfile.Weight <= 0)
		{
			CurrentBac = 0;
			BacText = "0.000 %";
			SoberTimeText = "Várható teljes kijózanodás: -";
			if (currentProfile?.BackgroundColorEnabled ?? true)
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
		if (currentProfile?.BackgroundColorEnabled ?? true)
		{
			MainBackgroundColor = GetBackgroundColor(CurrentBac);
			MainTextColor = GetReadableTextColor(MainBackgroundColor);
		}

		if (CurrentBac <= 0)
		{
			SoberTimeText = "Várható teljes kijózanodás: -";
			if (currentProfile?.BackgroundColorEnabled ?? true)
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

	void UpdateConsumptionVisualStates(DateTime referenceTime, bool allowVibration)
	{
		bool shouldVibrate = false;

		if (currentProfile == null || currentProfile.Weight <= 0)
		{
			foreach (var record in ConsumptionList)
			{
				record.TextDecorations = TextDecorations.None;
				record.EmptyingProgressText = string.Empty;
				record.IsEmptyingProgressVisible = false;
				record.IsEmpty = false;
			}

			return;
		}

		double genderFactor = currentProfile.Gender == "Nő" ? 0.6 : 0.7;
		ConsumptionRecord? activeRecord = GetActiveEmptyingRecord(referenceTime, genderFactor);

		foreach (var record in ConsumptionList)
		{
			bool wasEmpty = record.TextDecorations == TextDecorations.Strikethrough;
			double remainingBac = GetCurrentBacContribution(record, referenceTime, currentProfile.Weight, genderFactor);
			bool isEmpty = remainingBac <= 0;
			record.IsEmpty = isEmpty;
			record.TextDecorations = isEmpty
				? TextDecorations.Strikethrough
				: TextDecorations.None;

			bool isActiveRecord = activeRecord != null && ReferenceEquals(record, activeRecord) && !isEmpty;
			record.IsEmptyingProgressVisible = isActiveRecord;
			record.EmptyingProgressText = isActiveRecord
				? $"Ürülés: {GetEmptyingProgressPercent(record, referenceTime, currentProfile.Weight, genderFactor):0.#}%"
				: string.Empty;

			if (allowVibration && !wasEmpty && isEmpty)
			{
				shouldVibrate = true;
			}
		}

		if (shouldVibrate)
		{
			TryVibrate();
		}
	}

	ConsumptionRecord? GetActiveEmptyingRecord(DateTime referenceTime, double genderFactor)
	{
		if (currentProfile == null || currentProfile.Weight <= 0)
		{
			return null;
		}

		foreach (var record in ConsumptionList.OrderBy(item => item.Timestamp))
		{
			if (GetCurrentBacContribution(record, referenceTime, currentProfile.Weight, genderFactor) > 0)
			{
				return record;
			}
		}

		return null;
	}

	double GetEmptyingProgressPercent(ConsumptionRecord item, DateTime referenceTime, double weight, double genderFactor)
	{
		double initialBac = (item.AmountMl * (item.AlcoholPercent / 100.0) * 0.789) / (weight * genderFactor);

		if (initialBac <= 0)
		{
			return 100;
		}

		double elapsedHours = Math.Max(0, (referenceTime - item.Timestamp).TotalHours);
		double totalEmptyHours = initialBac / BacEliminationRatePerHour;

		if (totalEmptyHours <= 0)
		{
			return 100;
		}

		return Math.Clamp((elapsedHours / totalEmptyHours) * 100, 0, 100);
	}

	void TryVibrate()
	{
		if (!(currentProfile?.VibrationEnabled ?? true))
		{
			return;
		}

		try
		{
			if (Vibration.Default.IsSupported)
			{
				Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(80));
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine("HIBA REZGÉS KÖZBEN: " + ex.Message);
		}
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

	public void RefreshSummary(bool updateConsumptionVisuals = true)
	{
		RecalculateSummary(updateConsumptionVisuals, false);
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
					string latitude = location.Latitude.ToString("0.#####", CultureInfo.InvariantCulture);
					string longitude = location.Longitude.ToString("0.#####", CultureInfo.InvariantCulture);
					string mapsLink = $"https://www.google.com/maps/search/?api=1&query={latitude},{longitude}";

					return $"Aktuális pozíció: {mapsLink}";
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