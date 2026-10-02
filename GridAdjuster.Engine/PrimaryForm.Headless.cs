/***************************************************************************
 *   macOS port © 2026 GramzeSweatshop                                     *
 *   GNU GPLv2 or later, see LICENSE.                                      *
 ***************************************************************************/
// The headless half of Mootilda's PrimaryForm. It stands in for her
// PrimaryForm.Designer.cs, which is pure WinForms layout and does not come
// to macOS. Her PrimaryForm.cs is compiled UNCHANGED beside it.
//
// InitializeComponent below declares the same controls under the same names
// (as stand-ins from WinFormsHeadless.cs), applies the same starting values
// (her resx strings, Visible/Enabled flags and layout via PrimaryForm.resx,
// plus the values her Designer set in code), and wires exactly the same
// event handlers her Designer wired, in the same order.
//
// The controls are public so a host (the Avalonia app, the SmokeTest) can
// read and set them. A host drives her form the way a user did: pick list
// entries, set the range and elevation boxes, check a Pattern, then
// PerformClick() her buttons.

using System;
using System.Globalization;
using System.Resources;

namespace LotExpander
{
    public partial class PrimaryForm : Form
    {
        #region Controls (names and types as in PrimaryForm.Designer.cs)
        public Label Title;
        public ListBox Liste;
        public Button NextButton;
        public Button BackButton;
        public TextBox Explanation;
        public Button AdvancedButton;
        public Panel Specifications;
        public GroupBox Range;
        public Label FromLabel;
        public Label ToLabel;
        public Label LevelLabel;
        public NumericUpDown FromLevel;
        public NumericUpDown ToLevel;
        public Label FrontLabel;
        public NumericUpDown FromFront;
        public NumericUpDown ToFront;
        public Label LeftLabel;
        public NumericUpDown FromLeft;
        public NumericUpDown ToLeft;
        public GroupBox Pattern;
        public RadioButton PatternBasement;
        public RadioButton PatternFlat;
        public RadioButton PatternSloped;
        public RadioButton PatternCurved;
        public RadioButton PatternHyPar;
        public ProgressBar Progress;
        public GroupBox Elevation;
        public Panel FlatPanel;
        public Label OffsetLabel;
        public NumericUpDown Offset;
        public Panel BasementPanel;
        public CheckBox RemoveBasement;
        public CheckBox AdvancedBasementOptions;
        public Panel AdvancedBasementPanel;
        public CheckBox AdjustWater;
        public NumericUpDown WaterOffset;
        public CheckBox AdjustGround;
        public NumericUpDown GroundOffset;
        public CheckBox NoMajorVertex;
        public Panel SlopedPanel;
        public Label SlopedLabel;
        public NumericUpDown SlopedOffset;
        public Panel CurvedPanel;
        public Label CurvedAmpLabel;
        public NumericUpDown CurvedAmplitude;
        public Label CurvedPhaseLabel;
        public NumericUpDown CurvedPhase;
        public Label CurvedPeriodLabel;
        public NumericUpDown CurvedPeriod;
        public Panel MultiLevelPanel;
        public Label MultLabel;
        public NumericUpDown Multiplier;
        public Panel RotatePanel;
        public Label RotateLabel;
        public NumericUpDown Rotate;
        public Label RotateDesc;
        public CheckBox Relative;
        public CheckBox MultiBackup;
        public Button LotCatalog;
        public CheckBox ValuesInClicks;
        #endregion

        private ResourceManager _designerResources;

        private string Res(string key) => _designerResources.GetString(key, CultureInfo.InvariantCulture);

        private T Make<T>(string name) where T : Control, new()
        {
            var c = new T { Name = name };
            // ApplyResources: the Text, Visible and Enabled her resx holds.
            string text = Res(name + ".Text");
            if (text != null) c.Text = text;
            string visible = Res(name + ".Visible");
            if (visible != null) c.Visible = bool.Parse(visible);
            string enabled = Res(name + ".Enabled");
            if (enabled != null) c.Enabled = bool.Parse(enabled);
            // Layout values, for hosts that draw the form.
            string location = Res(name + ".Location");
            if (location != null) { var v = Pair(location); c.Location = new System.Drawing.Point(v.Item1, v.Item2); }
            string size = Res(name + ".Size");
            if (size != null) { var v = Pair(size); c.Size = new System.Drawing.Size(v.Item1, v.Item2); }
            c.Font = Res(name + ".Font");
            c.TextAlign = Res(name + ".TextAlign");
            c.Multiline = Res(name + ".Multiline") == "True";
            c.AutoSize = Res(name + ".AutoSize") == "True";
            return c;
        }

        private static (int, int) Pair(string s)
        {
            string[] p = s.Split(',');
            return (int.Parse(p[0].Trim(), CultureInfo.InvariantCulture), int.Parse(p[1].Trim(), CultureInfo.InvariantCulture));
        }

        // Her Designer's order for a NumericUpDown: Maximum, Minimum, Value.
        private static void Numeric(NumericUpDown n, decimal max, decimal min, decimal? value = null, int decimals = 0)
        {
            n.DecimalPlaces = decimals;
            n.Maximum = max;
            n.Minimum = min;
            if (value.HasValue) n.Value = value.Value;
        }

        private void InitializeComponent()
        {
            _designerResources = new ResourceManager("LotExpander.PrimaryForm", typeof(PrimaryForm).Assembly);
            Name = "PrimaryForm";
            Text = Res("$this.Text") ?? "TS2GridAdjuster";

            Title = Make<Label>("Title");
            Liste = Make<ListBox>("Liste");
            NextButton = Make<Button>("NextButton");
            BackButton = Make<Button>("BackButton");
            Explanation = Make<TextBox>("Explanation");
            AdvancedButton = Make<Button>("AdvancedButton");
            Specifications = Make<Panel>("Specifications");
            Range = Make<GroupBox>("Range");
            FromLabel = Make<Label>("FromLabel");
            ToLabel = Make<Label>("ToLabel");
            LevelLabel = Make<Label>("LevelLabel");
            FromLevel = Make<NumericUpDown>("FromLevel");
            ToLevel = Make<NumericUpDown>("ToLevel");
            FrontLabel = Make<Label>("FrontLabel");
            FromFront = Make<NumericUpDown>("FromFront");
            ToFront = Make<NumericUpDown>("ToFront");
            LeftLabel = Make<Label>("LeftLabel");
            FromLeft = Make<NumericUpDown>("FromLeft");
            ToLeft = Make<NumericUpDown>("ToLeft");
            Pattern = Make<GroupBox>("Pattern");
            PatternBasement = Make<RadioButton>("PatternBasement");
            PatternFlat = Make<RadioButton>("PatternFlat");
            PatternSloped = Make<RadioButton>("PatternSloped");
            PatternCurved = Make<RadioButton>("PatternCurved");
            PatternHyPar = Make<RadioButton>("PatternHyPar");
            Progress = Make<ProgressBar>("Progress");
            Elevation = Make<GroupBox>("Elevation");
            FlatPanel = Make<Panel>("FlatPanel");
            OffsetLabel = Make<Label>("OffsetLabel");
            Offset = Make<NumericUpDown>("Offset");
            BasementPanel = Make<Panel>("BasementPanel");
            RemoveBasement = Make<CheckBox>("RemoveBasement");
            AdvancedBasementOptions = Make<CheckBox>("AdvancedBasementOptions");
            AdvancedBasementPanel = Make<Panel>("AdvancedBasementPanel");
            AdjustWater = Make<CheckBox>("AdjustWater");
            WaterOffset = Make<NumericUpDown>("WaterOffset");
            AdjustGround = Make<CheckBox>("AdjustGround");
            GroundOffset = Make<NumericUpDown>("GroundOffset");
            NoMajorVertex = Make<CheckBox>("NoMajorVertex");
            SlopedPanel = Make<Panel>("SlopedPanel");
            SlopedLabel = Make<Label>("SlopedLabel");
            SlopedOffset = Make<NumericUpDown>("SlopedOffset");
            CurvedPanel = Make<Panel>("CurvedPanel");
            CurvedAmpLabel = Make<Label>("CurvedAmpLabel");
            CurvedAmplitude = Make<NumericUpDown>("CurvedAmplitude");
            CurvedPhaseLabel = Make<Label>("CurvedPhaseLabel");
            CurvedPhase = Make<NumericUpDown>("CurvedPhase");
            CurvedPeriodLabel = Make<Label>("CurvedPeriodLabel");
            CurvedPeriod = Make<NumericUpDown>("CurvedPeriod");
            MultiLevelPanel = Make<Panel>("MultiLevelPanel");
            MultLabel = Make<Label>("MultLabel");
            Multiplier = Make<NumericUpDown>("Multiplier");
            RotatePanel = Make<Panel>("RotatePanel");
            RotateLabel = Make<Label>("RotateLabel");
            Rotate = Make<NumericUpDown>("Rotate");
            RotateDesc = Make<Label>("RotateDesc");
            Relative = Make<CheckBox>("Relative");
            MultiBackup = Make<CheckBox>("MultiBackup");
            LotCatalog = Make<Button>("LotCatalog");
            ValuesInClicks = Make<CheckBox>("ValuesInClicks");

            // Values her Designer set in code rather than through the resx.
            Explanation.BackColor = System.Drawing.SystemColors.Control;
            Explanation.BorderNone = true;
            Explanation.ReadOnly = true;
            ToLevel.Value = 100;
            ToFront.Value = 100;
            ToLeft.Value = 100;
            Numeric(Offset, 999, -99, decimals: 4);
            AdjustWater.Checked = true;
            Numeric(WaterOffset, 999, -1664, decimals: 4);
            AdjustGround.Checked = true;
            Numeric(GroundOffset, 999, -1664, decimals: 4);
            NoMajorVertex.Checked = true;
            Numeric(SlopedOffset, 999, -99, decimals: 4);
            Numeric(CurvedAmplitude, 999, 0.0001m, 0.75m, 4);
            Numeric(CurvedPhase, 99, -99);
            Numeric(CurvedPeriod, 99, 1, 10);
            Numeric(Multiplier, 999, -99, 3, 4);
            Rotate.Increment = 90;
            Rotate.Maximum = 270;
            string clientSize = Res("$this.ClientSize");
            if (clientSize != null) { var v = Pair(clientSize); Size = new System.Drawing.Size(v.Item1, v.Item2); }

            // Containment, in her Designer's Controls.Add order.
            Specifications.Add(Range, Pattern, Progress, Elevation);
            Range.Add(FromLabel, ToLabel, LevelLabel, FromLevel, ToLevel, FrontLabel, FromFront, ToFront,
                LeftLabel, FromLeft, ToLeft);
            Pattern.Add(PatternBasement, PatternFlat, PatternSloped, PatternCurved, PatternHyPar);
            Elevation.Add(FlatPanel, BasementPanel, SlopedPanel, CurvedPanel, MultiLevelPanel, RotatePanel, Relative);
            FlatPanel.Add(OffsetLabel, Offset);
            BasementPanel.Add(RemoveBasement, AdvancedBasementOptions, AdvancedBasementPanel);
            AdvancedBasementPanel.Add(AdjustWater, WaterOffset, AdjustGround, GroundOffset, NoMajorVertex);
            SlopedPanel.Add(SlopedLabel, SlopedOffset);
            CurvedPanel.Add(CurvedAmpLabel, CurvedAmplitude, CurvedPhaseLabel, CurvedPhase, CurvedPeriodLabel,
                CurvedPeriod);
            MultiLevelPanel.Add(MultLabel, Multiplier);
            RotatePanel.Add(RotateLabel, Rotate, RotateDesc);
            Add(Title, Specifications, MultiBackup, ValuesInClicks, BackButton, LotCatalog, AdvancedButton,
                NextButton, Liste, Explanation);

            // Event wiring, in her Designer's order.
            Liste.SelectedIndexChanged += new EventHandler(Liste_SelectedIndexChanged);
            Liste.DoubleClick += new EventHandler(Liste_DoubleClick);
            Liste.KeyDown += new KeyEventHandler(Liste_KeyDown);
            NextButton.Click += new EventHandler(NextButton_Click);
            BackButton.Click += new EventHandler(BackButton_Click);
            AdvancedButton.Click += new EventHandler(AdvancedButton_Click);
            FromLevel.ValueChanged += new EventHandler(LevelChanged);
            ToLevel.ValueChanged += new EventHandler(LevelChanged);
            FromFront.ValueChanged += new EventHandler(DepthChanged);
            ToFront.ValueChanged += new EventHandler(DepthChanged);
            FromLeft.ValueChanged += new EventHandler(WidthChanged);
            ToLeft.ValueChanged += new EventHandler(WidthChanged);
            PatternBasement.CheckedChanged += new EventHandler(PatternChanged);
            PatternFlat.CheckedChanged += new EventHandler(PatternChanged);
            PatternSloped.CheckedChanged += new EventHandler(PatternChanged);
            PatternCurved.CheckedChanged += new EventHandler(PatternChanged);
            PatternHyPar.CheckedChanged += new EventHandler(PatternChanged);
            Offset.ValueChanged += new EventHandler(ElevChanged);
            RemoveBasement.CheckedChanged += new EventHandler(RemoveBasement_CheckedChanged);
            AdvancedBasementOptions.CheckedChanged += new EventHandler(AdvancedBasementOptions_CheckedChanged);
            AdjustWater.CheckedChanged += new EventHandler(AdjustWater_CheckedChanged);
            AdjustGround.CheckedChanged += new EventHandler(AdjustGround_CheckedChanged);
            Rotate.ValueChanged += new EventHandler(Rotate_ValueChanged);
            MultiBackup.CheckedChanged += new EventHandler(MultiBackup_CheckedChanged);
            LotCatalog.Click += new EventHandler(LotCatalog_Click);
            ValuesInClicks.CheckedChanged += new EventHandler(ValuesInClicks_CheckedChanged);
            Load += new EventHandler(PrimaryForm_Load);
            Shown += new EventHandler(PrimaryForm_Shown);
            FormClosing += new FormClosingEventHandler(PrimaryForm_FormClosing);
        }

        #region Host API (not in her form)
        // What her neighborhood list did on double-click, for a host that
        // already knows the package: set NBPack, then show the Lot screen.
        public void OpenNeighborhood(string packagePath)
        {
            if (NBPack != null)
                NBPack.Close(true);
            bUseLotCatalog = false;
            NBPack = SimPe.Packages.File.LoadFromFile(packagePath);
            this.Tag = 0;
            Screen = Screen_Neighborhood;
            LotScreen();
        }

        public int CurrentScreen => Screen;
        public const int ScreenInitial = Screen_Initial;
        public const int ScreenNeighborhood = Screen_Neighborhood;
        public const int ScreenLot = Screen_Lot;
        public const int ScreenLotCatalog = Screen_LotCatalog;
        public const int ScreenSpecification = Screen_Specification;
        public const int ScreenFinal = Screen_Final;

        // The lot package the Specification screen opened (null before).
        public string LotPackageName => LotPackName;

        // The lot's front direction (her U11), set by the Specification screen.
        public byte LotRotation => U11;

        // Lowest level on the lot (her WGRARes.MinimumLevel); her grid array
        // index 0 is this level.
        public int LotMinimumLevel => WGRARes?.MinimumLevel ?? 0;

        public string NeighborhoodFileName => NBPack?.FileName;
        #endregion
    }
}
