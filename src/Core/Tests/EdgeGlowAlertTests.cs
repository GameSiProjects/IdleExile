using System;
using System.Linq;
using Godot;
using IdleExile.Core.Models;
using IdleExile.Core.Services;
using IdleExile.UI;

namespace IdleExile.Core.Tests;

/// <summary>
/// Unit tests for Task 6.2: Viền Hào Quang Thông Báo (Edge Glow Alerts & Audio Chime).
/// Verifies Tier 1 drop recognition, DropTier evaluation, and procedural audio synthesis.
/// </summary>
public static class EdgeGlowAlertTests
{
    public static void RunAll()
    {
        GD.Print("[Tests] Running EdgeGlowAlertTests (Task 6.2)...");

        TestTier1CurrencyDropRecognition();
        TestTier1EquipmentAndGemRecognition();
        TestDropTierClassification();
        TestProceduralTingAudioSynthesis();

        GD.Print("[Tests] All EdgeGlowAlertTests PASSED successfully!");
    }

    private static void TestTier1CurrencyDropRecognition()
    {
        // 1. High value currencies must qualify as Tier 1
        var divine = LootDrop.Currency("divine_orb", "Divine Orb", colorHex: "#E8D882");
        var exalt = LootDrop.Currency("exalted_orb", "Exalted Orb", colorHex: "#D4AC0D");
        var mirror = LootDrop.Currency("mirror_of_kalandra", "Mirror of Kalandra", colorHex: "#F9E79F");
        var vaal = LootDrop.Currency("vaal_orb", "Vaal Orb", colorHex: "#C0392B");

        AssertTrue(LootDropEvaluationHelper.IsTier1Drop(divine), "Divine Orb must qualify as Tier 1 drop");
        AssertTrue(LootDropEvaluationHelper.IsTier1Drop(exalt), "Exalted Orb must qualify as Tier 1 drop");
        AssertTrue(LootDropEvaluationHelper.IsTier1Drop(mirror), "Mirror of Kalandra must qualify as Tier 1 drop");
        AssertTrue(LootDropEvaluationHelper.IsTier1Drop(vaal), "Vaal Orb must qualify as Tier 1 drop");

        // 2. Normal / Basic currencies must NOT qualify as Tier 1
        var wisdom = LootDrop.Currency("scroll_of_wisdom", "Scroll of Wisdom", colorHex: "#AA9E82");
        var transmute = LootDrop.Currency("orb_of_transmutation", "Orb of Transmutation", colorHex: "#5DADE2");
        var chaos = LootDrop.Currency("chaos_orb", "Chaos Orb", colorHex: "#E67E22");

        AssertFalse(LootDropEvaluationHelper.IsTier1Drop(wisdom), "Scroll of Wisdom is not Tier 1");
        AssertFalse(LootDropEvaluationHelper.IsTier1Drop(transmute), "Orb of Transmutation is not Tier 1");
        AssertFalse(LootDropEvaluationHelper.IsTier1Drop(chaos), "Chaos Orb is High Tier but not Tier 1 holy grail");
    }

    private static void TestTier1EquipmentAndGemRecognition()
    {
        // 1. 6-Link Equipment must qualify as Tier 1
        var sixLinkBody = LootDrop.Equipment("astral_plate_6l", "Astral Plate (6-Link)", LootRarity.Rare);
        var sixLinkDesc = new LootDrop
        {
            Id = "glorious_plate",
            DisplayName = "Glorious Plate",
            Type = LootDropType.Equipment,
            Rarity = LootRarity.Rare,
            Description = "Item with 6-Link sockets"
        };
        AssertTrue(LootDropEvaluationHelper.IsTier1Drop(sixLinkBody), "6-Link in DisplayName must qualify as Tier 1");
        AssertTrue(LootDropEvaluationHelper.IsTier1Drop(sixLinkDesc), "6-Link in Description must qualify as Tier 1");

        // 2. Unique rarity items qualify as Tier 1
        var uniqueItem = LootDrop.Equipment("tabula_rasa", "Tabula Rasa", LootRarity.Unique);
        AssertTrue(LootDropEvaluationHelper.IsTier1Drop(uniqueItem), "Unique rarity items qualify as Tier 1");

        // 3. Regular Rare/Magic equipment without 6-link does NOT qualify as Tier 1
        var regularRare = LootDrop.Equipment("astral_plate", "Astral Plate", LootRarity.Rare);
        var normalArmor = LootDrop.Equipment("iron_cuirass", "Iron Cuirass", LootRarity.Normal);
        AssertFalse(LootDropEvaluationHelper.IsTier1Drop(regularRare), "Standard Rare armor is not Tier 1");
        AssertFalse(LootDropEvaluationHelper.IsTier1Drop(normalArmor), "Normal armor is not Tier 1");

        // 4. Fully socketed active gem (5 support slots = 6-Link equivalent)
        var maxGem = LootDrop.Gem("cleave_5s", "Cleave (5-Slot)", colorHex: "#E74C3C");
        var sixLinkGem = LootDrop.Gem("fireball_6l", "Fireball (6-Link)", colorHex: "#E67E22");
        var standardGem = LootDrop.Gem("split_arrow", "Split Arrow", colorHex: "#2ECC71");

        AssertTrue(LootDropEvaluationHelper.IsTier1Drop(maxGem), "5-Slot Active Gem qualifies as Tier 1");
        AssertTrue(LootDropEvaluationHelper.IsTier1Drop(sixLinkGem), "6-Link Active Gem qualifies as Tier 1");
        AssertFalse(LootDropEvaluationHelper.IsTier1Drop(standardGem), "Standard skill gem is not Tier 1");
    }

    private static void TestDropTierClassification()
    {
        var divine = LootDrop.Currency("divine_orb", "Divine Orb");
        var chaos = LootDrop.Currency("chaos_orb", "Chaos Orb");
        var alteration = LootDrop.Currency("orb_of_alteration", "Orb of Alteration");
        var wisdom = LootDrop.Currency("scroll_of_wisdom", "Scroll of Wisdom");
        var map = LootDrop.Map("strand", "Strand Map", tier: 1);

        AssertEqual(DropTier.Tier1, LootDropEvaluationHelper.EvaluateTier(divine), "Divine Orb is Tier 1");
        AssertEqual(DropTier.High, LootDropEvaluationHelper.EvaluateTier(chaos), "Chaos Orb is High Tier");
        AssertEqual(DropTier.Mid, LootDropEvaluationHelper.EvaluateTier(alteration), "Alteration Orb is Mid Tier");
        AssertEqual(DropTier.Normal, LootDropEvaluationHelper.EvaluateTier(wisdom), "Scroll of Wisdom is Normal Tier");
        AssertEqual(DropTier.High, LootDropEvaluationHelper.EvaluateTier(map), "Map slates are High Tier");
    }

    private static void TestProceduralTingAudioSynthesis()
    {
        // 1. Verify AudioStreamWav generation
        var chimeStream = DropSoundSynthesizer.Tier1DropChime;
        AssertNotNull(chimeStream, "Tier1DropChime must not be null");
        AssertEqual(44100, chimeStream.MixRate, "MixRate should be 44100 Hz");
        AssertFalse(chimeStream.Stereo, "Stream should be mono");
        AssertEqual(AudioStreamWav.FormatEnum.Format16Bits, chimeStream.Format, "Format must be 16-bit PCM");
        AssertEqual(AudioStreamWav.LoopModeEnum.Disabled, chimeStream.LoopMode, "LoopMode must be disabled");

        // 2. Verify PCM data buffer length and amplitude
        byte[] pcmData = chimeStream.Data;
        AssertNotNull(pcmData, "PCM byte data must not be null");

        int expectedSamples = (int)(DropSoundSynthesizer.DefaultSampleRate * DropSoundSynthesizer.Tier1ChimeDuration);
        int expectedBytes = expectedSamples * 2;
        AssertEqual(expectedBytes, pcmData.Length, "PCM buffer length must match sample rate and duration");

        // 3. Verify audio waveform contains non-zero sound samples (not silence)
        bool hasSignificantAmplitude = false;
        for (int i = 0; i < pcmData.Length - 1; i += 2)
        {
            short sample = (short)(pcmData[i] | (pcmData[i + 1] << 8));
            if (Math.Abs(sample) > 5000) // Meaningful bell strike amplitude
            {
                hasSignificantAmplitude = true;
                break;
            }
        }

        AssertTrue(hasSignificantAmplitude, "Synthesized chime must contain audible high-amplitude bell strike data");
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!System.Collections.Generic.EqualityComparer<T>.Default.Equals(expected, actual))
            throw new Exception($"Assertion Failed: Expected '{expected}', but was '{actual}'. {message}");
    }

    private static void AssertNotNull(object? obj, string message)
    {
        if (obj == null) throw new Exception($"Assertion Failed: {message} (Object was null)");
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition) throw new Exception($"Assertion Failed: {message}");
    }

    private static void AssertFalse(bool condition, string message)
    {
        if (condition) throw new Exception($"Assertion Failed: {message}");
    }
}
