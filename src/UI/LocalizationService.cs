using System;
using Godot;

namespace IdleExile.UI;

/// <summary>
/// Centralized Localization service for Idle Exile.
/// Integrates with Godot's TranslationServer and CSV-based translation tables.
/// Ensures all in-game text defaults to English ("en"), with ready support for future locale switching (e.g. "vi").
/// </summary>
public static class LocalizationService
{
    public const string DefaultLocale = "en";

    /// <summary>
    /// Gets the current active locale (e.g., "en", "vi").
    /// </summary>
    public static string CurrentLocale => TranslationServer.GetLocale();

    /// <summary>
    /// Translates the given key or text using Godot's active TranslationServer.
    /// If no translation is found, returns the key itself as fallback.
    /// </summary>
    public static string Tr(string key)
    {
        if (string.IsNullOrEmpty(key)) return string.Empty;
        var translated = TranslationServer.Translate(key);
        return string.IsNullOrEmpty(translated) ? key : (string)translated;
    }

    /// <summary>
    /// Formats a translated string with positional arguments.
    /// </summary>
    public static string Format(string key, params object[] args)
    {
        var template = Tr(key);
        try
        {
            return string.Format(template, args);
        }
        catch
        {
            return template;
        }
    }

    /// <summary>
    /// Sets the active locale for the application.
    /// </summary>
    public static void SetLocale(string locale)
    {
        TranslationServer.SetLocale(locale);
    }
}

/// <summary>
/// Shorthand alias for LocalizationService.
/// </summary>
public static class Loc
{
    public static string Tr(string key) => LocalizationService.Tr(key);
    public static string Format(string key, params object[] args) => LocalizationService.Format(key, args);
    public static void SetLocale(string locale) => LocalizationService.SetLocale(locale);
}
