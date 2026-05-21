using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace AlkoLog;

public partial class HistoryPageViewModel : ObservableObject
{
	public ObservableCollection<ConsumptionRecord> HistoryList { get; private set; }

	[ObservableProperty]
	private ConsumptionRecord selectedItem;

	string historyFilePath = Path.Combine(FileSystem.Current.AppDataDirectory, "history.json");

	public HistoryPageViewModel()
	{
		HistoryList = new ObservableCollection<ConsumptionRecord>();
	}

	public void AddRecords(IEnumerable<ConsumptionRecord> records)
	{
		if (records == null)
		{
			return;
		}

		foreach (var record in records)
		{
			PrepareForHistory(record);
			HistoryList.Add(record);
		}
	}

	public void ResetData()
	{
		HistoryList.Clear();
		SelectedItem = null;
	}

	[RelayCommand]
	async Task DeleteSelectedAsync()
	{
		if (SelectedItem == null)
		{
			return;
		}

		HistoryList.Remove(SelectedItem);
		SelectedItem = null;
		await SaveDataAsync();
	}

	public async Task LoadDataAsync()
	{
		try
		{
			if (File.Exists(historyFilePath) && HistoryList.Count == 0)
			{
				string jsonText = await File.ReadAllTextAsync(historyFilePath);
				var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
				var items = JsonSerializer.Deserialize<List<ConsumptionRecord>>(jsonText, options);

				if (items != null)
				{
					foreach (var item in items.OrderBy(item => item.Timestamp))
					{
						PrepareForHistory(item);
						HistoryList.Add(item);
					}
				}
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine("HIBA ELŐZMÉNYEK BETÖLTÉSKOR: " + ex.Message);
		}
	}

	public async Task SaveDataAsync()
	{
		try
		{
			string jsonText = JsonSerializer.Serialize(HistoryList);
			await File.WriteAllTextAsync(historyFilePath, jsonText);
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine("HIBA ELŐZMÉNYEK MENTÉSKOR: " + ex.Message);
		}
	}

	void PrepareForHistory(ConsumptionRecord record)
	{
		record.TextDecorations = TextDecorations.None;
		record.EmptyingProgressText = string.Empty;
		record.IsEmptyingProgressVisible = false;
	}
}
