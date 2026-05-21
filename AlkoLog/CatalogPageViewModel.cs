using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json;

namespace AlkoLog;

[QueryProperty(nameof(ReceivedItem), "newdrink")]
public partial class CatalogPageViewModel : ObservableObject
{
	public ObservableCollection<DrinkCatalogItem> DrinkCatalog { get; private set; }

	readonly TimeSpan _rapidTapWindow = TimeSpan.FromMilliseconds(1200);
	DrinkCatalogItem? _lastRapidTappedItem;
	int _rapidTapCount;
	DateTime _lastRapidTapUtc = DateTime.MinValue;

	[ObservableProperty]
	private DrinkCatalogItem selectedItem;

	readonly MainPageViewModel _mainPageViewModel;

	string filePath = Path.Combine(FileSystem.Current.AppDataDirectory, "catalog.json");

	public CatalogPageViewModel(MainPageViewModel mainPageViewModel)
	{
		_mainPageViewModel = mainPageViewModel;
		DrinkCatalog = new ObservableCollection<DrinkCatalogItem>();
	}

	public DrinkCatalogItem ReceivedItem
	{
		set
		{
			if (value != null)
			{
				if (string.IsNullOrWhiteSpace(value.ImagePath))
				{
					value.ImagePath = "ital.png";
				}

				HookItem(value);
				DrinkCatalog.Add(value);
				SortCatalog();
			}
		}
	}

	[RelayCommand]
	async Task AddNewAsync()
	{
		await Shell.Current.GoToAsync("newdrink");
	}

	[RelayCommand]
	async Task EditSelectedAsync()
	{
		if (SelectedItem != null)
		{
			var navigationParameter = new ShellNavigationQueryParameters
			{
				{ "editdrink", SelectedItem },
				{ "editmode", true }
			};

			await Shell.Current.GoToAsync("newdrink", navigationParameter);
		}
	}

	[RelayCommand]
	void DeleteSelected()
	{
		if (SelectedItem != null)
		{
			UnhookItem(SelectedItem);
			DrinkCatalog.Remove(SelectedItem);
			SelectedItem = null;
		}
	}

	[RelayCommand]
	async Task ShareSelectedAsync()
	{
		if (SelectedItem != null)
		{
			string shareText = $"Ital: {SelectedItem.Name} - {SelectedItem.AlcoholPercent}%";

			await Share.Default.RequestAsync(new ShareTextRequest
			{
				Title = "Italkatalógus megosztása",
				Text = shareText
			});
		}
	}

	[RelayCommand]
	async Task DrinkSelectedAsync()
	{
		if (SelectedItem == null)
		{
			return;
		}

		try
		{
			string amountText = await Shell.Current.DisplayPromptAsync(
				"Ital megivása",
				"Mennyi millilitert ittál meg?",
				"Rögzítés",
				"Mégse",
				"50",
				keyboard: Keyboard.Numeric);

			if (string.IsNullOrWhiteSpace(amountText))
			{
				return;
			}

			double.TryParse(amountText, out double amountMl);

			await AddConsumptionRecordAsync(SelectedItem, amountMl, navigateHome: true);
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine("HIBA ITAL RÖGZÍTÉSKOR: " + ex.Message);
		}
	}

	[RelayCommand]
	async Task QuickAddDrinkAsync(DrinkCatalogItem tappedDrink)
	{
		if (tappedDrink == null)
		{
			return;
		}

		DateTime now = DateTime.UtcNow;

		if (ReferenceEquals(_lastRapidTappedItem, tappedDrink) && now - _lastRapidTapUtc <= _rapidTapWindow)
		{
			_rapidTapCount++;
		}
		else
		{
			_lastRapidTappedItem = tappedDrink;
			_rapidTapCount = 1;
		}

		_lastRapidTapUtc = now;

		if (_rapidTapCount < 5)
		{
			return;
		}

		ResetRapidTapTracking();
		await AddConsumptionRecordAsync(tappedDrink, 50, navigateHome: false);
	}

	async Task AddConsumptionRecordAsync(DrinkCatalogItem drink, double amountMl, bool navigateHome)
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

		var record = new ConsumptionRecord
		{
			DrinkName = drink.Name,
			AlcoholPercent = drink.AlcoholPercent,
			AmountMl = amountMl,
			Timestamp = DateTime.Now,
			Latitude = latitude,
			Longitude = longitude
		};

		_mainPageViewModel.AddConsumption(record);
		await _mainPageViewModel.SaveDataAsync();
		_mainPageViewModel.RefreshSummary();

		if (navigateHome)
		{
			await Shell.Current.GoToAsync("//MainPage");
		}
	}

	void ResetRapidTapTracking()
	{
		_lastRapidTappedItem = null;
		_rapidTapCount = 0;
		_lastRapidTapUtc = DateTime.MinValue;
	}

	void HookItem(DrinkCatalogItem item)
	{
		item.PropertyChanged += DrinkCatalogItem_PropertyChanged;
	}

	void UnhookItem(DrinkCatalogItem item)
	{
		item.PropertyChanged -= DrinkCatalogItem_PropertyChanged;
	}

	void DrinkCatalogItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(DrinkCatalogItem.IsFavorite))
		{
			SortCatalog();
		}
	}

	void SortCatalog()
	{
		var currentSelectedItem = SelectedItem;
		var sortedItems = DrinkCatalog
			.OrderByDescending(item => item.IsFavorite)
			.ThenBy(item => item.Name)
			.ToList();

		DrinkCatalog.Clear();

		foreach (var item in sortedItems)
		{
			DrinkCatalog.Add(item);
		}

		SelectedItem = currentSelectedItem;
	}

	public void ResortCatalog()
	{
		SortCatalog();
	}

	public async Task ResetDataAsync()
	{
		try
		{
			foreach (var item in DrinkCatalog.ToList())
			{
				UnhookItem(item);
			}

			DrinkCatalog.Clear();
			SelectedItem = null;

			AddDefaultItems();
			await SaveDataAsync();
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine("HIBA KATALÓGUS ALAPHELYZETBE ÁLLÍTÁSKOR: " + ex.Message);
		}
	}

	void AddDefaultItems()
	{
		var defaultItems = new List<DrinkCatalogItem>
		{
			new DrinkCatalogItem { Name = "Világos sör", AlcoholPercent = 5.0, ImagePath = "ital.png" },
			new DrinkCatalogItem { Name = "Vörösbor", AlcoholPercent = 12.5, ImagePath = "ital.png" },
			new DrinkCatalogItem { Name = "Házi pálinka", AlcoholPercent = 50.0, ImagePath = "ital.png" }
		};

		foreach (var item in defaultItems)
		{
			HookItem(item);
			DrinkCatalog.Add(item);
		}

		SortCatalog();
	}

	public async Task SaveDataAsync()
	{
		try
		{
			string jsonText = JsonSerializer.Serialize(DrinkCatalog);
			await File.WriteAllTextAsync(filePath, jsonText);
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine("HIBA KATALÓGUS MENTÉSKOR: " + ex.Message);
		}
	}

	public async Task LoadDataAsync()
	{
		try
		{
			if (File.Exists(filePath) && DrinkCatalog.Count == 0)
			{
				string jsonText = await File.ReadAllTextAsync(filePath);
				var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
				var items = JsonSerializer.Deserialize<List<DrinkCatalogItem>>(jsonText, options);

				if (items != null)
				{
					foreach (var item in items)
					{
						if (string.IsNullOrWhiteSpace(item.ImagePath))
						{
							item.ImagePath = "ital.png";
						}

						HookItem(item);
						DrinkCatalog.Add(item);
					}

					SortCatalog();
				}
			}

			if (DrinkCatalog.Count == 0)
			{
				AddDefaultItems();
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine("HIBA KATALÓGUS BETÖLTÉSKOR: " + ex.Message);
			if (DrinkCatalog.Count == 0)
			{
				AddDefaultItems();
			}
		}
	}
}