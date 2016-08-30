﻿using QSP.AircraftProfiles;
using QSP.AircraftProfiles.Configs;
using QSP.AviationTools;
using QSP.Common;
using QSP.LibraryExtension;
using QSP.Utilities.Units;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using static QSP.MathTools.Doubles;
using static QSP.UI.Utilities.MsgBoxHelper;
using static QSP.LibraryExtension.Paths;

namespace QSP.UI.ToLdgModule.AircraftMenu
{
    public class AcMenuController
    {
        public const string NoToLdgProfileText = "None";

        public event EventHandler AircraftsChanged;

        private AcMenuElements elem;
        private ProfileManager profiles;
        private AircraftConfig currentConfig;

        public AcMenuController(AcMenuElements elem, ProfileManager profiles)
        {
            this.elem = elem;
            this.profiles = profiles;
        }

        public void InitializeControls()
        {
            elem.SelectionBox.Location = new Point(0, 0);
            elem.PropertyBox.Location = new Point(0, 0);
            ShowSelectionGroupBox();

            InitWtUnitCBox();
        }

        private void FillFuelTOLdgCBox()
        {
            var fuelItems = elem.FuelProfile.Items;
            fuelItems.Clear();
            fuelItems.Add(NoToLdgProfileText);
            fuelItems.AddRange(profiles.FuelData
                .Select(t => t.ProfileName)
                .Distinct()
                .OrderBy(s => s)
                .ToArray());
            elem.FuelProfile.SelectedIndex = 0;

            var toItems = elem.ToProfile.Items;
            toItems.Clear();
            toItems.Add(NoToLdgProfileText);
            toItems.AddRange(profiles.TOTables
                .Select(t => t.Entry.ProfileName)
                .Distinct()
                .OrderBy(s => s)
                .ToArray());
            elem.ToProfile.SelectedIndex = 0;

            var ldgItems = elem.LdgProfile.Items;
            ldgItems.Clear();
            ldgItems.Add(NoToLdgProfileText);
            ldgItems.AddRange(profiles.LdgTables
                .Select(t => t.Entry.ProfileName)
                .Distinct()
                .OrderBy(s => s)
                .ToArray());
            elem.LdgProfile.SelectedIndex = 0;
        }

        private void InitWtUnitCBox()
        {
            var cbox = elem.WeightUnitCBox;
            cbox.Items.Clear();
            cbox.Items.AddRange(new string[] { "KG", "LB" });
            cbox.SelectedIndex = 0;

            cbox.SelectedIndexChanged += WtUnitChanged;
        }

        private void WtUnitChanged(object sender, EventArgs e)
        {
            var factor = elem.WeightUnitCBox.SelectedIndex == 0 ?
                Constants.LbKgRatio :
                Constants.KgLbRatio;

            var textBoxes = new TextBox[] { elem.Zfw,
                elem.MaxToWt, elem.MaxLdgWt, elem.MaxZfw };

            foreach (var j in textBoxes)
            {
                double wt;

                if (double.TryParse(j.Text, out wt))
                {
                    j.Text = RoundToInt(wt * factor).ToString();
                }
            }
        }

        private void FillAcTypes()
        {
            var acItems = elem.AcType.Items;
            acItems.Clear();

            var ac = profiles.AcConfigs.Aircrafts;

            var acTypes =
                ac.Select(x => x.Config.AC)
                .Distinct()
                .OrderBy(s => s)
                .ToArray();

            acItems.AddRange(acTypes);
        }

        private void RefreshListView()
        {
            var listItems = elem.AcListView.Items;
            listItems.Clear();

            var ac = profiles.AcConfigs.Aircrafts.ToList();
            ac.Sort(new ConfigComparer());

            foreach (var i in ac)
            {
                var c = i.Config;

                var lvi = new ListViewItem(c.AC);
                lvi.SubItems.Add(c.Registration);
                listItems.Add(lvi);
            }

            ListViewSelectedChanged(null, null);
            elem.AcListView.Columns[1].Width = -2;
        }

        private void FillProperties(AircraftConfigItem config)
        {
            var e = elem;
            var c = config;

            e.AcType.Text = c.AC;
            e.Registration.Text = c.Registration;
            e.FuelProfile.Text = c.FuelProfile;
            e.ToProfile.Text = c.TOProfile;
            e.LdgProfile.Text = c.LdgProfile;
            e.WeightUnitCBox.SelectedIndex = (int)config.WtUnit;
            e.Zfw.Text = WtDisplay(c.OewKg);
            e.MaxToWt.Text = WtDisplay(c.MaxTOWtKg);
            e.MaxLdgWt.Text = WtDisplay(c.MaxLdgWtKg);
            e.MaxZfw.Text = WtDisplay(c.MaxZfwKg);
        }

        private void ShowDefaultConfig()
        {
            FillProperties(defaultAcConfig);
        }

        private AircraftConfigItem defaultAcConfig
        {
            get
            {
                return new AircraftConfigItem("", "", NoToLdgProfileText,
                    NoToLdgProfileText, NoToLdgProfileText, 0.0, 0.0,
                    0.0, 0.0, WeightUnit.KG);
            }
        }

        private string WtDisplay(double weightKg)
        {
            if (elem.WeightUnitCBox.SelectedIndex == 0)
            {
                // KG
                return RoundToInt(weightKg).ToString();
            }
            else
            {
                // LB
                return RoundToInt(weightKg * Constants.KgLbRatio).ToString();
            }
        }

        public void CreateConfig(object sender, EventArgs e)
        {
            ShowPropertyGroupBox();
            ShowDefaultConfig();
            currentConfig = null;
        }

        private string selectedRegistration
        {
            get
            {
                var selected = elem.AcListView.SelectedItems;

                if (selected.Count == 0)
                {
                    return null;
                }

                return selected[0].SubItems[1].Text;
            }
        }

        private void ShowSelectionGroupBox()
        {
            elem.SelectionBox.Visible = true;
            elem.PropertyBox.Visible = false;

            RefreshListView();
        }

        private void ShowPropertyGroupBox()
        {
            elem.PropertyBox.Visible = true;
            elem.SelectionBox.Visible = false;

            FillAcTypes();
            FillFuelTOLdgCBox();
        }

        private bool TrySaveConfig(AircraftConfigItem config, string filePath)
        {
            try
            {
                ConfigSaver.Save(config, filePath);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void EditConfig(object sender, EventArgs e)
        {
            var reg = selectedRegistration;

            if (reg != null)
            {
                currentConfig = profiles.AcConfigs.Find(reg);

                ShowPropertyGroupBox();
                FillProperties(currentConfig.Config);
            }
        }

        private void RemoveOldConfig()
        {
            if (inEditMode)
            {
                profiles.AcConfigs.Remove(currentConfig.Config.Registration);
            }
        }

        // If false, then user is creating a new config.
        private bool inEditMode
        {
            get
            {
                return currentConfig != null;
            }
        }

        /// <exception cref="NoFileNameAvailException"></exception>
        private string GetFileName()
        {
            if (inEditMode == false)
            {
                return GenerateFileName();
            }
            else
            {
                var filePath = currentConfig.FilePath;
                var directory = GetUri(Path.GetDirectoryName(filePath));
                var defaultDirectory = GetUri(ConfigLoader.DefaultFolderPath);

                if (directory.Equals(defaultDirectory))
                {
                    return GenerateFileName();
                }

                return currentConfig.FilePath;
            }
        }

        private string GenerateFileName()
        {
            var nameBase =
                    (elem.AcType.Text + "_" + elem.Registration.Text)
                    .RemoveIllegalChars();

            return FileNameGenerator.Generate(
                ConfigLoader.UserDefinedFolderPath,
                nameBase,
                (i) => "_" + i.ToString(),
                ".ini");
        }

        private AircraftConfigItem TryValidate()
        {
            try
            {
                return new AcConfigValidator(elem).Validate();
            }
            catch (InvalidUserInputException ex)
            {
                ShowWarning(ex.Message);
                return null;
            }
        }

        private string TryGetFileName()
        {
            try
            {
                return GetFileName();
            }
            catch (NoFileNameAvailException)
            {
                // FileNameGenerator cannot generate a file name.
                ShowError("Failed to save config file.");
                return null;
            }
        }

        public void SaveConfig(object sender, EventArgs e)
        {
            var config = TryValidate();

            if (config == null)
            {
                return;
            }

            if (inEditMode == false &&
                profiles.AcConfigs.Find(config.Registration) != null)
            {
                ShowWarning(
                   "Registration already exists. Please use another one.");
                return;
            }

            var fn = TryGetFileName();

            if (fn == null) return;
            
            if (TrySaveConfig(config, fn))
            {
                RemoveOldConfig();
                profiles.AcConfigs.Add(new AircraftConfig(config, fn));
                ShowSelectionGroupBox();
                AircraftsChanged?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                ShowError("Failed to save config file.");
            }
        }

        public void DeleteConfig(object sender, EventArgs e)
        {
            var reg = selectedRegistration;

            if (reg == null)
            {
                return;
            }

            var configs = profiles.AcConfigs;
            var item = configs.Find(reg);
            var path = item.FilePath;
            var ac = item.Config.AC;

            var result =
                MessageBox.Show(
                    $"Permanently delete {reg} ({ac}) ?",
                    "",
                    MessageBoxButtons.YesNoCancel,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2);

            if (result == DialogResult.Yes &&
                TryDeleteConfig(path))
            {
                configs.Remove(reg);
                RefreshListView();
                AircraftsChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private bool TryDeleteConfig(string path)
        {
            try
            {
                File.Delete(path);
                return true;
            }
            catch
            {
                ShowError("Failed to delete the selected config.");
                return false;
            }
        }

        private bool ChangesMade()
        {
            AircraftConfigItem config = null;

            try
            {
                config = new AcConfigValidator(elem).Read();
            }
            catch
            {
                return true;
            }

            const double delta = 1.0;

            if (inEditMode)
            {
                return !config.Equals(currentConfig.Config, delta);
            }
            else
            {
                return !config.Equals(defaultAcConfig, delta);
            }
        }

        public void CancelBtnClicked(object sender, EventArgs e)
        {
            if (ChangesMade() == false)
            {
                // No edit is done.
                // No need to show messageBox.
                ShowSelectionGroupBox();
                return;
            }

            var result =
                MessageBox.Show(
                    "Discard the changes to config?",
                    "",
                    MessageBoxButtons.YesNoCancel,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2);

            if (result == DialogResult.Yes)
            {
                ShowSelectionGroupBox();
            }
        }

        public void ListViewSelectedChanged(object sender, EventArgs e)
        {
            var edit = elem.EditBtn;
            var del = elem.DeleteBtn;

            if (elem.AcListView.SelectedIndices.Count == 0)
            {
                edit.ForeColor = Color.White;
                edit.Enabled = false;
                del.ForeColor = Color.White;
                del.Enabled = false;
            }
            else
            {
                edit.Enabled = true;
                edit.ForeColor = Color.Black;
                del.Enabled = true;
                del.ForeColor = Color.Black;
            }
        }
    }
}
