using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Utilities;

namespace MatchGridExtents
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    [Autodesk.Revit.DB.Macros.AddInId("4524a1b8-b55a-48d8-974f-38fa3a377290")]
    public class ThisApplication : IExternalApplication
    {
        public Result OnShutdown(UIControlledApplication uiApp)
        {
            return Result.Succeeded;
        }

        public Result OnStartup(UIControlledApplication uiApp)
        {

            try
            {
                #region GAS ADDIN BOILERPLATE
                // Assembly that contains the invoke method
                string exeConfigPath = Utils.GetExeConfigPath("MatchGridExtents.dll");

                // Finds and creates the tab, finds and creates the panel
                RibbonPanel DefaultPanel = Utils.GetRevitPanel(uiApp, GlobalVars.PANEL_NAME);
                #endregion

                // Button configuration
                string MatchGridExtentsName = "Default\nName";
                PushButtonData MatchGridExtentsData = new PushButtonData(MatchGridExtentsName, MatchGridExtentsName, exeConfigPath, "MatchGridExtents.ThisCommand");
                MatchGridExtentsData.LargeImage = Utils.RetriveImage("MatchGridExtents.Resources.MatchGridExtents32x32.ico", Assembly.GetExecutingAssembly()); // Pushbutton image
                MatchGridExtentsData.ToolTip = "";
                DefaultPanel.AddItem(MatchGridExtentsData); // Add pushbutton

                return Result.Succeeded;
            }


            catch (Exception ex)
            {
                Utils.CatchDialog(ex);
                return Result.Failed;
            }
        }
    }
}
