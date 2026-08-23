using System;
using System.Diagnostics;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Windowing;
using GatherBuddy.CustomInfo;

namespace GatherBuddy.Gui;

public sealed class FishStrategyWindow : Window
{
    public const string WindowId = "钓鱼攻略###GatherBuddyFishStrategyWindowCompact";

    private Interface.ExtendedFish? _fish;
    private FishStrategyEntry?      _strategy;

    public FishStrategyWindow()
        : base(WindowId)
    {
        Size          = VulcanUiScaling.Scaled(500f, 260f);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = Vector2.Zero,
            MaximumSize = VulcanUiScaling.Scaled(800f, 700f),
        };
        ShowCloseButton   = true;
        RespectCloseHotkey = true;
        IsOpen             = false;
    }

    public void Open(Interface.ExtendedFish fish)
    {
        if (!FishStrategyCatalog.TryGet(fish.Data, out var strategy))
            return;

        _fish     = fish;
        _strategy = strategy;
        IsOpen    = true;
    }

    public override void PreDraw()
    {
        if (_fish != null)
            WindowName = $"{_fish.Data.Name[GatherBuddy.Language]} - 钓法###GatherBuddyFishStrategyWindowCompact";
    }

    public override void Draw()
    {
        if (_fish == null || _strategy == null)
        {
            ImGui.TextDisabled("没有可显示的钓法。");
            return;
        }

        ImGui.BeginChild("##FishStrategyScroll", Vector2.Zero, false);
        try
        {
            ImGui.TextColored(ImGuiColors.DalamudOrange, "钓法");
            ImGui.Spacing();

            foreach (var paragraph in _strategy.Paragraphs)
            {
                FishStrategyTextRenderer.DrawParagraph(paragraph, ImGui.GetContentRegionAvail().X);
                ImGui.Spacing();
            }

            ImGui.Separator();
            ImGui.Spacing();
            ImGui.TextDisabled(_strategy.Author == null ? $"来源: {FishStrategyCatalog.SourceCredit}" : $"作者: {_strategy.Author}");
            if (_strategy.Author != null)
                ImGui.TextDisabled($"来源: {FishStrategyCatalog.SourceCredit}");

            DrawSourceButtons(_strategy);
        }
        finally
        {
            ImGui.EndChild();
        }
    }

    private static void DrawSourceButtons(FishStrategyEntry strategy)
    {
        var buttonSize = new Vector2(ImGui.GetFrameHeight());
        bool copy;
        using (ElliLib.Raii.ImRaii.PushFont(UiBuilder.IconFont))
            copy = ImGui.Button(FontAwesomeIcon.Copy.ToIconString() + "##CopyFishStrategy", buttonSize);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("复制钓法");
        if (copy)
            ImGui.SetClipboardText(string.Join("\n\n", strategy.Paragraphs));

        ImGui.SameLine();
        bool openSource;
        using (ElliLib.Raii.ImRaii.PushFont(UiBuilder.IconFont))
            openSource = ImGui.Button(FontAwesomeIcon.ExternalLinkAlt.ToIconString() + "##OpenFishStrategySource", buttonSize);
        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.PushTextWrapPos(ImGui.GetFontSize() * 35f);
            ImGui.TextUnformatted(FishStrategyCatalog.SourceTitle);
            ImGui.PopTextWrapPos();
            ImGui.EndTooltip();
        }
        if (!openSource)
            return;

        try
        {
            Process.Start(new ProcessStartInfo(FishStrategyCatalog.SourceUrl) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            GatherBuddy.Log.Warning($"打开钓鱼攻略来源失败: {ex.Message}");
        }
    }
}
