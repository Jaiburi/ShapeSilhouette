using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>
/// Creates the whole control panel in code (world-space canvas, rows, buttons, feedback text),
/// so the scene needs no manual UI setup. Works with XR Interaction Toolkit ray interactors.
/// </summary>
public class MatchUIBuilder : MonoBehaviour
{
    const float CanvasWidth = 800f;
    const float CanvasHeight = 680f;
    const float RowHeight = 56f;
    const float LabelWidth = 200f;

    static readonly Color AccentColor = new Color(0.20f, 0.50f, 0.95f);
    static readonly Color NeutralButtonColor = new Color(0.30f, 0.33f, 0.40f);
    static readonly Color ConfirmButtonColor = new Color(0.15f, 0.65f, 0.35f);
    static readonly Color PanelColor = new Color(0.08f, 0.09f, 0.12f, 0.92f);

    struct OptionButton
    {
        public Image image;
        public Text text;
        public Color baseColor;
        public System.Func<bool> isSelected;
    }

    readonly List<OptionButton> options = new List<OptionButton>();
    readonly Text[] rotationValueTexts = new Text[3];

    MatchGameController game;
    Font font;
    Text feedbackText;
    Text attemptsText;
    Text confirmLabel;

    // ---------------------------------------------------------------- Public API

    public void Build(MatchGameController controller, Vector3 position, Quaternion rotation)
    {
        game = controller;
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        EnsureXRUIEventSystem();

        // Canvas
        var canvasGO = new GameObject("MatchCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasGO.transform.SetPositionAndRotation(position, rotation);

        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
        canvasGO.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 3f;
        canvasGO.AddComponent<TrackedDeviceGraphicRaycaster>(); // lets XR ray interactors click the UI

        var canvasRect = (RectTransform)canvasGO.transform;
        canvasRect.sizeDelta = new Vector2(CanvasWidth, CanvasHeight);
        canvasRect.localScale = Vector3.one * 0.001f; // 800 px -> 0.8 m

        // Background
        var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(canvasGO.transform, false);
        Stretch((RectTransform)bg.transform);
        var bgImage = bg.GetComponent<Image>();
        bgImage.color = PanelColor;
        bgImage.raycastTarget = true;

        // Vertical content layout
        var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup));
        content.transform.SetParent(canvasGO.transform, false);
        Stretch((RectTransform)content.transform);
        var vertical = content.GetComponent<VerticalLayoutGroup>();
        vertical.padding = new RectOffset(24, 24, 16, 16);
        vertical.spacing = 8f;
        vertical.childControlWidth = true;
        vertical.childControlHeight = true;
        vertical.childForceExpandWidth = true;
        vertical.childForceExpandHeight = false;

        Transform root = content.transform;

        // Title + attempts
        Transform titleRow = AddRow(root, 44f);
        Text title = CreateText(titleRow, "Match the target object", 34, TextAnchor.MiddleLeft, FontStyle.Bold);
        SetLayout(title.gameObject, -1f, 1f);
        attemptsText = CreateText(titleRow, "", 26, TextAnchor.MiddleRight, FontStyle.Normal);
        SetLayout(attemptsText.gameObject, 200f, 0f);

        // Shape / Color / Size
        AddOptionRow(root, "Shape", new[] { "Cube", "Sphere", "Cylinder" },
            i => game.SetShape((ShapeType)i),
            i => game.Current.shape == (ShapeType)i,
            i => AccentColor);

        Color[] swatches =
        {
            new Color(0.90f, 0.20f, 0.20f),
            new Color(0.20f, 0.75f, 0.30f),
            new Color(0.20f, 0.40f, 0.95f)
        };
        AddOptionRow(root, "Color", new[] { "Red", "Green", "Blue" },
            i => game.SetColor((ColorType)i),
            i => game.Current.color == (ColorType)i,
            i => swatches[i]);

        AddOptionRow(root, "Size", new[] { "Small", "Medium", "Large" },
            i => game.SetSize((SizeType)i),
            i => game.Current.size == (SizeType)i,
            i => AccentColor);

        // Rotation rows
        AddRotationRow(root, 0, "X");
        AddRotationRow(root, 1, "Y");
        AddRotationRow(root, 2, "Z");

        // Reset / Confirm
        Transform actionRow = AddRow(root, 64f);
        AddButton(actionRow, "Reset", game.ResetObject, NeutralButtonColor, 1f, out _);
        AddButton(actionRow, "Confirm", game.Confirm, ConfirmButtonColor, 1.4f, out confirmLabel);

        // Feedback
        feedbackText = CreateText(root, "", 30, TextAnchor.MiddleCenter, FontStyle.Bold);
        feedbackText.horizontalOverflow = HorizontalWrapMode.Wrap;
        feedbackText.verticalOverflow = VerticalWrapMode.Truncate;
        SetLayout(feedbackText.gameObject, -1f, 1f, 100f);

        game.Changed += Refresh;
        Refresh();
    }

    public void CreateLabel(string text, Vector3 position, Quaternion rotation)
    {
        var go = new GameObject(text + "Label", typeof(RectTransform), typeof(Canvas));
        go.transform.SetPositionAndRotation(position, rotation);
        go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;

        var rect = (RectTransform)go.transform;
        rect.sizeDelta = new Vector2(500f, 100f);
        rect.localScale = Vector3.one * 0.001f;

        Text label = CreateText(go.transform, text, 60, TextAnchor.MiddleCenter, FontStyle.Bold);
        Stretch(label.rectTransform);
    }

    // ---------------------------------------------------------------- Refresh

    void Refresh()
    {
        foreach (OptionButton option in options)
        {
            bool selected = option.isSelected();
            Color c = selected ? option.baseColor : option.baseColor * 0.4f;
            c.a = 1f;
            option.image.color = c;
            option.text.fontStyle = selected ? FontStyle.Bold : FontStyle.Normal;
        }

        Vector3 euler = game.Current.eulerDegrees;
        for (int i = 0; i < 3; i++)
        {
            int degrees = Mathf.RoundToInt(Mathf.Repeat(euler[i], 360f)) % 360;
            rotationValueTexts[i].text = degrees + "\u00B0";
        }

        attemptsText.text = "Attempts: " + game.Attempts;
        confirmLabel.text = game.Solved ? "Next target" : "Confirm";
        feedbackText.text = game.FeedbackMessage;

        switch (game.FeedbackKind)
        {
            case FeedbackKind.Success: feedbackText.color = new Color(0.35f, 0.95f, 0.45f); break;
            case FeedbackKind.Error: feedbackText.color = new Color(1.00f, 0.65f, 0.25f); break;
            default: feedbackText.color = new Color(0.85f, 0.88f, 0.95f); break;
        }
    }

    void OnDestroy()
    {
        if (game != null) game.Changed -= Refresh;
    }

    // ---------------------------------------------------------------- Row builders

    void AddOptionRow(Transform parent, string label, string[] names,
        System.Action<int> onPick, System.Func<int, bool> isSelected, System.Func<int, Color> baseColor)
    {
        Transform row = AddRow(parent, RowHeight);
        AddRowLabel(row, label);

        for (int i = 0; i < names.Length; i++)
        {
            int index = i; // capture for the closures below
            Button button = AddButton(row, names[i], () => onPick(index), baseColor(index), 1f, out Text text);
            options.Add(new OptionButton
            {
                image = button.image,
                text = text,
                baseColor = baseColor(index),
                isSelected = () => isSelected(index)
            });
        }
    }

    void AddRotationRow(Transform parent, int axis, string axisName)
    {
        Transform row = AddRow(parent, RowHeight);
        AddRowLabel(row, "Rotate " + axisName);

        string step = game.rotationStepDegrees.ToString("0") + "\u00B0";
        AddButton(row, "-" + step, () => game.RotateAxis(axis, -1f), NeutralButtonColor, 1f, out _);

        Text value = CreateText(row, "0\u00B0", 30, TextAnchor.MiddleCenter, FontStyle.Bold);
        SetLayout(value.gameObject, 120f, 0f);
        rotationValueTexts[axis] = value;

        AddButton(row, "+" + step, () => game.RotateAxis(axis, 1f), NeutralButtonColor, 1f, out _);
    }

    Transform AddRow(Transform parent, float height)
    {
        var go = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        go.transform.SetParent(parent, false);

        var layout = go.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 10f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;

        go.GetComponent<LayoutElement>().preferredHeight = height;
        return go.transform;
    }

    void AddRowLabel(Transform row, string text)
    {
        Text label = CreateText(row, text, 30, TextAnchor.MiddleLeft, FontStyle.Normal);
        SetLayout(label.gameObject, LabelWidth, 0f);
    }

    Button AddButton(Transform parent, string label, UnityAction onClick, Color color, float flexibleWidth, out Text text)
    {
        var go = new GameObject(label + "Button", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        SetLayout(go, -1f, flexibleWidth);

        var image = go.GetComponent<Image>();
        image.color = color;

        var button = go.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(onClick);

        // Visible hover/press feedback for the VR ray
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(0.75f, 0.90f, 1f);
        colors.pressedColor = new Color(0.60f, 0.80f, 1f);
        button.colors = colors;

        text = CreateText(go.transform, label, 28, TextAnchor.MiddleCenter, FontStyle.Normal);
        Stretch(text.rectTransform);
        return button;
    }

    // ---------------------------------------------------------------- Helpers

    Text CreateText(Transform parent, string content, int size, TextAnchor anchor, FontStyle style)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);

        var text = go.GetComponent<Text>();
        text.font = font;
        text.text = content;
        text.fontSize = size;
        text.alignment = anchor;
        text.fontStyle = style;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    static void SetLayout(GameObject go, float preferredWidth, float flexibleWidth, float preferredHeight = -1f)
    {
        var element = go.GetComponent<LayoutElement>();
        if (element == null) element = go.AddComponent<LayoutElement>();
        if (preferredWidth >= 0f) element.preferredWidth = preferredWidth;
        element.flexibleWidth = flexibleWidth;
        if (preferredHeight >= 0f) element.preferredHeight = preferredHeight;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    /// <summary>
    /// World-space UI needs an EventSystem driven by XRUIInputModule so ray interactors
    /// can click it. Any other input module is disabled to avoid conflicts.
    /// </summary>
    static void EnsureXRUIEventSystem()
    {
        EventSystem eventSystem = FindFirstObjectByType<EventSystem>();
        if (eventSystem == null)
        {
            eventSystem = new GameObject("EventSystem", typeof(EventSystem)).GetComponent<EventSystem>();
        }

        if (eventSystem.GetComponent<XRUIInputModule>() == null)
        {
            eventSystem.gameObject.AddComponent<XRUIInputModule>();
        }

        foreach (BaseInputModule module in eventSystem.GetComponents<BaseInputModule>())
        {
            if (!(module is XRUIInputModule)) module.enabled = false;
        }
    }
}
