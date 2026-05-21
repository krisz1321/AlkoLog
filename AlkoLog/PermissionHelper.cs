using Microsoft.Maui.ApplicationModel;

namespace AlkoLog;

public static class PermissionHelper
{
	public static async Task<bool> EnsureCameraPermissionAsync()
	{
		var status = await Permissions.CheckStatusAsync<Permissions.Camera>();

		if (status == PermissionStatus.Granted)
		{
			return true;
		}

		status = await Permissions.RequestAsync<Permissions.Camera>();
		return status == PermissionStatus.Granted;
	}

	public static async Task<bool> EnsureLocationPermissionAsync()
	{
		var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();

		if (status == PermissionStatus.Granted)
		{
			return true;
		}

		status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
		return status == PermissionStatus.Granted;
	}
}