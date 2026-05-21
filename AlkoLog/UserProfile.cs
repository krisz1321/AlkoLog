namespace AlkoLog;

public class UserProfile
{
    public string Name { get; set; } = string.Empty;

    public int Age { get; set; }

    public double Weight { get; set; }

    public string Gender { get; set; } = string.Empty;

    public string PhotoPath { get; set; } = string.Empty;

    public bool VibrationEnabled { get; set; } = true;
}