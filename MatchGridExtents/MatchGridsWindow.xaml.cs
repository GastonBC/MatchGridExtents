using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Utilities;

namespace MatchGridExtents
{
    public class ViewItem : INotifyPropertyChanged
    {
        public View View { get; set; }

        public bool _Checked;
        public ViewSheet Sheet { get; set; }
        public bool Checked
        {
            get { return _Checked; }
            set
            {
                if (_Checked == value)
                    return;
                _Checked = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Checked"));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }


    public partial class MatchGridsWindow : Window
    {
        private Document doc;
        private UIDocument uidoc;
        private readonly ObservableCollection<ViewItem> SourceViewColl = new ObservableCollection<ViewItem>();
        private View CorrectView;
        private List<Viewport> AllViewports;

        private readonly ObservableCollection<ViewItem> TargetViewsColl = new ObservableCollection<ViewItem>();


        public MatchGridsWindow(UIDocument uid)
        {
            InitializeComponent();
            uidoc = uid;
            doc = uid.Document;

            AllViewports = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Viewports)
                .WhereElementIsNotElementType()
                .Cast<Viewport>()
                .OrderBy(v => v.Name)
                .ToList();

            foreach (Viewport vp in AllViewports)
            {
                View sourceView = doc.GetElement(vp.ViewId) as View;

                if (sourceView.ViewType != ViewType.FloorPlan && sourceView.ViewType != ViewType.CeilingPlan) continue;

                ViewItem addition = new ViewItem
                {
                    Checked = false,
                    View = sourceView,
                    Sheet = (ViewSheet)doc.GetElement(vp.SheetId),
                };

                SourceViewColl.Add(addition);
            }
            lv_SourceView.ItemsSource = SourceViewColl;
            lv_Target.ItemsSource = TargetViewsColl;
        }


        private void SelSheets_Click(object sender, RoutedEventArgs e)
        {
            this.Close();


            //BUG: It's not unhiding grids
            using (Transaction t = new Transaction(doc, "Match grid layout"))
            {
                t.Start();

                List<View> ViewsToChange = TargetViewsColl.Where(v => v.Checked).Select(x => x.View).ToList();

                ElementId CropboxNone = new ElementId(-1);

                Parameter CorrectView_Param = CorrectView.LookupParameter("Scope Box");
                ElementId CorrectView_ScopeBox = CorrectView_Param.AsElementId();
                bool CorrectView_CropboxVisibleState = CorrectView.CropBoxVisible;
                bool CorrectView_CropboxActiveState = CorrectView.CropBoxActive;

                CropUncropView(CorrectView, CorrectView_Param, false, CropboxNone, true);

                foreach (View ViewToChange in ViewsToChange)
                {
                    //// Turn all grids on, then turn off hidden grids
                    //IEnumerable<Grid> GridsToHide = GetAllGrids().Where(g => g.IsHidden(CorrectView));

                    //if (GridsToHide.Count() > 0)
                    //{
                    //    // Unhide all grids
                    //    ViewToChange.UnhideElements(GetAllGrids().Where(g => g.IsHidden(ViewToChange))
                    //                                             .Select(g => g.Id)
                    //                                             .ToList()) ;

                    //    // Hide the first list
                    //    ViewToChange.HideElements(GridsToHide.Select(g => g.Id).ToList());
                    //}

                    // Set extent to 2D
                    foreach (Grid xGrid in GetVisibleGrids(ViewToChange))
                    {
                        xGrid.SetDatumExtentType(DatumEnds.End0, ViewToChange, DatumExtentType.ViewSpecific);
                        xGrid.SetDatumExtentType(DatumEnds.End1, ViewToChange, DatumExtentType.ViewSpecific);
                    }


                    // Propagate extents. This matches bubble, elbow and length from a source view to a target view.
                    // It needs to turn off cropboxes and remove scope boxes applied. These are re-applied later on

                    Parameter ViewToChange_Param = ViewToChange.LookupParameter("Scope Box");
                    ElementId ViewToChange_ScopeBox = ViewToChange_Param.AsElementId();
                    bool ViewToChange_CropboxVisibleState = ViewToChange.CropBoxVisible;
                    bool ViewToChange_CropboxActiveState = ViewToChange.CropBoxActive;


                    CropUncropView(ViewToChange, ViewToChange_Param, false, CropboxNone, true);

                    foreach (Grid grid in GetVisibleGrids(CorrectView))
                    {
                        grid.PropagateToViews(CorrectView, new[] { ViewToChange.Id }.ToHashSet());
                    }

                    // Set states of view back
                    CropUncropView(ViewToChange, ViewToChange_Param, ViewToChange_CropboxActiveState, ViewToChange_ScopeBox, ViewToChange_CropboxVisibleState);

                }

                CropUncropView(CorrectView, CorrectView_Param, CorrectView_CropboxActiveState, CorrectView_ScopeBox, CorrectView_CropboxVisibleState);



                t.Commit();
            }
        }

        private void CropUncropView(View ViewToChange, Parameter ViewToChange_Param, bool CropboxActiveState, ElementId ScopeBoxId, bool CropboxVisibleState)
        {
            // Set states
            ViewToChange.CropBoxActive = CropboxActiveState;
            ViewToChange_Param.Set(ScopeBoxId);
            ViewToChange.CropBoxVisible = CropboxVisibleState;
        }


        private void Target_ClearSearch_Click(object sender, RoutedEventArgs e)
        {
            Target_tb_Search.Clear();
            lv_Target.SelectedItems.Clear();
        }

        private void SelAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (ViewItem sheetElem in TargetViewsColl)
            {
                sheetElem.Checked = true;
            }
        }

        private void DeselAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (ViewItem sheetElem in TargetViewsColl)
            {
                sheetElem.Checked = false;
            }
        }


        // -----------

        private void search_txt_changed(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            lv_Target.ItemsSource = TargetViewsColl.Where(v => v.View.Name.ToLower().Contains(Target_tb_Search.Text.ToLower())
                                                            || v.Sheet.SheetNumber.ToLower().Contains(Target_tb_Search.Text.ToLower()));
        }

        private void onCheckBoxCheck(object sender, RoutedEventArgs e)
        {

            // deselect single item if a checkbox is checked or it will change your selection
            // as well
            if (lv_Target.SelectedItems.Count == 1)
            {
                lv_Target.SelectedItem = null;
            }

            else
            {
                foreach (object ListObj in lv_Target.SelectedItems)
                {
                    ViewItem ListItem = ListObj as ViewItem;
                    ListItem.Checked = true;
                }
            }
            CheckButtons();
        }

        private void onCheckBoxUncheck(object sender, RoutedEventArgs e)
        {
            // deselect single item if a checkbox is checked or it will change your selection
            // as well
            if (lv_Target.SelectedItems.Count == 1)
            {
                lv_Target.SelectedItem = null;
            }

            else
            {
                foreach (object ListObj in lv_Target.SelectedItems)
                {
                    ViewItem ListItem = ListObj as ViewItem;
                    ListItem.Checked = false;
                }
            }
            CheckButtons();
        }

        private void Source_ClearSearch_Click(object sender, RoutedEventArgs e)
        {
            Source_Search_tb.Clear();
            lv_SourceView.SelectedItem = null;
        }

        private void Source_SearchTxt_Changed(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            lv_SourceView.ItemsSource = SourceViewColl.Where(v => v.View.Name.ToLower().Contains(Source_Search_tb.Text.ToLower())
                                                               || v.Sheet.SheetNumber.ToLower().Contains(Source_Search_tb.Text.ToLower()));
        }

        private void Source_SelChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            CorrectView = ((ViewItem)lv_SourceView.SelectedItem).View;
            TargetViewsColl.Clear();

            if (lv_SourceView.SelectedItem == null) return;

            foreach (Viewport vp in AllViewports)
            {

                View view = doc.GetElement(vp.ViewId) as View;

                if (view.ViewType != ViewType.FloorPlan && view.ViewType != ViewType.CeilingPlan) continue;
                if (view.Id == CorrectView.Id) continue;

                ViewItem addition = new ViewItem
                {
                    Checked = false,
                    View = view,
                    Sheet = (ViewSheet)doc.GetElement(vp.SheetId),
                };

                TargetViewsColl.Add(addition);
            }
            CheckButtons();
        }

        private void CheckButtons()
        {
            if (lv_SourceView.SelectedItem != null && TargetViewsColl.Where(v => v.Checked).Count() > 0)
            {
                b_DeselAll.IsEnabled = true;
                b_SelAll.IsEnabled = true;
                b_SelSheets.IsEnabled = true;
                return;
            }

            b_DeselAll.IsEnabled = false;
            b_SelAll.IsEnabled = false;
            b_SelSheets.IsEnabled = false;
        }

        private List<Grid> GetVisibleGrids(View view)
        {
            List<Grid> GridsVisibleCollector = new FilteredElementCollector(doc, view.Id)
                        .OfCategory(BuiltInCategory.OST_Grids)
                        .WhereElementIsNotElementType()
                        .Cast<Grid>()
                        .ToList();

            return GridsVisibleCollector;
        }

        private List<Grid> GetAllGrids()
        {
            List<Grid> GridsCollector = new FilteredElementCollector(doc)
                        .OfCategory(BuiltInCategory.OST_Grids)
                        .WhereElementIsNotElementType()
                        .Cast<Grid>()
                        .ToList();

            return GridsCollector;
        }

        private void Target_SelChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            CheckButtons();
        }
    }
}
