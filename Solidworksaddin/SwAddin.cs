

//// For debugging only -> for creating .msi please undef Debug
#define Debug
//#undef Debug

using System;
using System.Runtime.InteropServices;
using System.Collections;
using System.Reflection;
using System.Windows.Forms;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swpublished;
using SolidWorks.Interop.swconst;
using SolidWorksTools;
using SolidWorksTools.File;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;



namespace Solidworksaddin
{
    /// <summary>
    /// Summary description for Solidworksaddin.
    /// </summary>
    [Guid("5d8a1f46-ea8c-4ddb-8581-0a52f24245a0"), ComVisible(true)]
    [SwAddin(
        Description = "Solidworksaddin description",
        Title = "Solidworksaddin",
        LoadAtStartup = true
        )]
    public class SwAddin : ISwAddin
    {
        #region Local Variables
        ISldWorks iSwApp = null;
        ICommandManager iCmdMgr = null;
        int addinID = 0;
        BitmapHandler iBmp;

        public const int mainCmdGroupID = 5;
        public const int mainItemID1 = 0;
        public const int mainItemID2 = 1;
        public const int mainItemID3 = 2;
        public const int mainItemID4 = 3;
        public const int flyoutGroupID = 91;

        #region Event Handler Variables
        Hashtable openDocs = new Hashtable();
        SolidWorks.Interop.sldworks.SldWorks SwEventPtr = null;
        #endregion

        #region Property Manager Variables
        UserPMPage ppage = null;
        #endregion


        // Public Properties
        public ISldWorks SwApp
        {
            get { return iSwApp; }
        }
        public ICommandManager CmdMgr
        {
            get { return iCmdMgr; }
        }

        public Hashtable OpenDocs
        {
            get { return openDocs; }
        }

        #endregion

       ///Will be registered in WiX Toolset
       #if (Debug)
        #region SolidWorks Registration 
        
        [ComRegisterFunctionAttribute]
        public static void RegisterFunction(Type t)
        {
            #region Get Custom Attribute: SwAddinAttribute
            SwAddinAttribute SWattr = null;
            Type type = typeof(SwAddin);

            foreach (System.Attribute attr in type.GetCustomAttributes(false))
            {
                if (attr is SwAddinAttribute)
                {
                    SWattr = attr as SwAddinAttribute;
                    break;
                }
            }

            #endregion

            try
            {
                Microsoft.Win32.RegistryKey hklm = Microsoft.Win32.Registry.LocalMachine;
                Microsoft.Win32.RegistryKey hkcu = Microsoft.Win32.Registry.CurrentUser;

                string keyname = "SOFTWARE\\SolidWorks\\Addins\\{" + t.GUID.ToString() + "}";
                Microsoft.Win32.RegistryKey addinkey = hklm.CreateSubKey(keyname);
                addinkey.SetValue(null, 0);

                addinkey.SetValue("Description", SWattr.Description);
                addinkey.SetValue("Title", SWattr.Title);

                keyname = "Software\\SolidWorks\\AddInsStartup\\{" + t.GUID.ToString() + "}";
                addinkey = hkcu.CreateSubKey(keyname);
                addinkey.SetValue(null, Convert.ToInt32(SWattr.LoadAtStartup), Microsoft.Win32.RegistryValueKind.DWord);
            }
            catch (System.NullReferenceException nl)
            {
                Console.WriteLine("There was a problem registering this dll: SWattr is null. \n\"" + nl.Message + "\"");
                System.Windows.Forms.MessageBox.Show("There was a problem registering this dll: SWattr is null.\n\"" + nl.Message + "\"");
            }

            catch (System.Exception e)
            {
                Console.WriteLine(e.Message);

                System.Windows.Forms.MessageBox.Show("There was a problem registering the function: \n\"" + e.Message + "\"");
            }
        }

        [ComUnregisterFunctionAttribute]
        public static void UnregisterFunction(Type t)
        {
            try
            {
                Microsoft.Win32.RegistryKey hklm = Microsoft.Win32.Registry.LocalMachine;
                Microsoft.Win32.RegistryKey hkcu = Microsoft.Win32.Registry.CurrentUser;

                string keyname = "SOFTWARE\\SolidWorks\\Addins\\{" + t.GUID.ToString() + "}";
                hklm.DeleteSubKey(keyname);

                keyname = "Software\\SolidWorks\\AddInsStartup\\{" + t.GUID.ToString() + "}";
                hkcu.DeleteSubKey(keyname);
            }
            catch (System.NullReferenceException nl)
            {
                Console.WriteLine("There was a problem unregistering this dll: " + nl.Message);
                System.Windows.Forms.MessageBox.Show("There was a problem unregistering this dll: \n\"" + nl.Message + "\"");
            }
            catch (System.Exception e)
            {
                Console.WriteLine("There was a problem unregistering this dll: " + e.Message);
                System.Windows.Forms.MessageBox.Show("There was a problem unregistering this dll: \n\"" + e.Message + "\"");
            }
        }
        
        #endregion
        #endif


        #region ISwAddin Implementation
        public SwAddin()
        {
        }

        public bool ConnectToSW(object ThisSW, int cookie)
        {
            iSwApp = (ISldWorks)ThisSW;
            addinID = cookie;

            //Setup callbacks
            iSwApp.SetAddinCallbackInfo(0, this, addinID);

            #region Setup the Command Manager
            iCmdMgr = iSwApp.GetCommandManager(cookie);
            AddCommandMgr();
            #endregion

            #region Setup the Event Handlers
            SwEventPtr = (SolidWorks.Interop.sldworks.SldWorks)iSwApp;
            openDocs = new Hashtable();
            AttachEventHandlers();
            #endregion

            #region Setup Sample Property Manager
            AddPMP();
            #endregion


            return true;
        }
       
        public bool DisconnectFromSW()
        {
            RemoveCommandMgr();
            RemovePMP();
            DetachEventHandlers();

            System.Runtime.InteropServices.Marshal.ReleaseComObject(iCmdMgr);
            iCmdMgr = null;
            System.Runtime.InteropServices.Marshal.ReleaseComObject(iSwApp);
            iSwApp = null;
            //The addin _must_ call GC.Collect() here in order to retrieve all managed code pointers 
            GC.Collect();
            GC.WaitForPendingFinalizers();

            GC.Collect();
            GC.WaitForPendingFinalizers();

            return true;
        }
        #endregion

        #region UI Methods
        public void AddCommandMgr()
        {
            ICommandGroup cmdGroup;
            if (iBmp == null)
                iBmp = new BitmapHandler();
            Assembly thisAssembly;
            int cmdIndex0, cmdIndex1,cmdIndex2,cmdIndex3;
            string Title = "Alex's Solidworks Addin", ToolTip = "Alex's Solidworks Addin";


            int[] docTypes = new int[]{(int)swDocumentTypes_e.swDocASSEMBLY,
                                       (int)swDocumentTypes_e.swDocDRAWING,
                                       (int)swDocumentTypes_e.swDocPART};

            thisAssembly = System.Reflection.Assembly.GetAssembly(this.GetType());


            int cmdGroupErr = 0;
            bool ignorePrevious = false;

            object registryIDs;
            //get the ID information stored in the registry
            bool getDataResult = iCmdMgr.GetGroupDataFromRegistry(mainCmdGroupID, out registryIDs);

            int[] knownIDs = new int[2] { mainItemID1, mainItemID2 };

            if (getDataResult)
            {
                if (!CompareIDs((int[])registryIDs, knownIDs)) //if the IDs don't match, reset the commandGroup
                {
                    ignorePrevious = true;
                }
            }

            cmdGroup = iCmdMgr.CreateCommandGroup2(mainCmdGroupID, Title, ToolTip, "", -1, ignorePrevious, ref cmdGroupErr);
            cmdGroup.LargeIconList = iBmp.CreateFileFromResourceBitmap("Solidworksaddin.ToolbarLarge.bmp", thisAssembly);
            cmdGroup.SmallIconList = iBmp.CreateFileFromResourceBitmap("Solidworksaddin.ToolbarSmall.bmp", thisAssembly);
            cmdGroup.LargeMainIcon = iBmp.CreateFileFromResourceBitmap("Solidworksaddin.MainIconLarge.bmp", thisAssembly);
            cmdGroup.SmallMainIcon = iBmp.CreateFileFromResourceBitmap("Solidworksaddin.MainIconSmall.bmp", thisAssembly);

            int menuToolbarOption = (int)(swCommandItemType_e.swMenuItem | swCommandItemType_e.swToolbarItem);
            cmdIndex0 = cmdGroup.AddCommandItem2("Print Active Sheet", -1, "Print the active Sheet to the default Folder", "Print Sheet", 0, "PrintactiveSheet", "", mainItemID1, menuToolbarOption);
            cmdIndex1 = cmdGroup.AddCommandItem2("Print Active Document ", -1, "Print Active Document with all Sheets", "Print Active Document", 2, "PrintActiveDocument", "", mainItemID2, menuToolbarOption);
            cmdIndex2 = cmdGroup.AddCommandItem2("Print all Files in Folder ", -1, "Print all Files in Folder", "Print all Files in Folder", 2, "Print_Files_in_Folder", "", mainItemID3, menuToolbarOption);
            cmdIndex3 = cmdGroup.AddCommandItem2("Show PMP", -1, "Display sample property manager", "Show PMP", 2, "ShowPMP", "EnablePMP", mainItemID4, menuToolbarOption);

            cmdGroup.HasToolbar = true;
            cmdGroup.HasMenu = true;
            cmdGroup.Activate();

            bool bResult;



            FlyoutGroup flyGroup = iCmdMgr.CreateFlyoutGroup(flyoutGroupID, "Dynamic Flyout", "Flyout Tooltip", "Flyout Hint",
              cmdGroup.SmallMainIcon, cmdGroup.LargeMainIcon, cmdGroup.SmallIconList, cmdGroup.LargeIconList, "FlyoutCallback", "FlyoutEnable");


            flyGroup.AddCommandItem("FlyoutCommand 1", "test", 0, "FlyoutCommandItem1", "FlyoutEnableCommandItem1");

            flyGroup.FlyoutType = (int)swCommandFlyoutStyle_e.swCommandFlyoutStyle_Simple;


            foreach (int type in docTypes)
            {
                CommandTab cmdTab;

                cmdTab = iCmdMgr.GetCommandTab(type, Title);

                if (cmdTab != null & !getDataResult | ignorePrevious)//if tab exists, but we have ignored the registry info (or changed command group ID), re-create the tab.  Otherwise the ids won't matchup and the tab will be blank
                {
                    bool res = iCmdMgr.RemoveCommandTab(cmdTab);
                    cmdTab = null;
                }

                //if cmdTab is null, must be first load (possibly after reset), add the commands to the tabs
                if (cmdTab == null)
                {
                    cmdTab = iCmdMgr.AddCommandTab(type, Title);

                    CommandTabBox cmdBox = cmdTab.AddCommandTabBox();

                    int[] cmdIDs = new int[5];
                    int[] TextType = new int[5];

                    cmdIDs[0] = cmdGroup.get_CommandID(cmdIndex0);

                    TextType[0] = (int)swCommandTabButtonTextDisplay_e.swCommandTabButton_TextHorizontal;

                    cmdIDs[1] = cmdGroup.get_CommandID(cmdIndex1);

                    TextType[1] = (int)swCommandTabButtonTextDisplay_e.swCommandTabButton_TextHorizontal;

                    cmdIDs[2] = cmdGroup.get_CommandID(cmdIndex2);

                    TextType[2] = (int)swCommandTabButtonTextDisplay_e.swCommandTabButton_TextHorizontal;

                    cmdIDs[3] = cmdGroup.get_CommandID(cmdIndex3);

                    TextType[3] = (int)swCommandTabButtonTextDisplay_e.swCommandTabButton_TextHorizontal;


                    cmdIDs[4] = cmdGroup.ToolbarId;

                    TextType[4] = (int)swCommandTabButtonTextDisplay_e.swCommandTabButton_TextHorizontal | (int)swCommandTabButtonFlyoutStyle_e.swCommandTabButton_ActionFlyout;

                    

                    bResult = cmdBox.AddCommands(cmdIDs, TextType);



                    CommandTabBox cmdBox1 = cmdTab.AddCommandTabBox();
                    cmdIDs = new int[1];
                    TextType = new int[1];

                    cmdIDs[0] = flyGroup.CmdID;
                    TextType[0] = (int)swCommandTabButtonTextDisplay_e.swCommandTabButton_TextBelow | (int)swCommandTabButtonFlyoutStyle_e.swCommandTabButton_ActionFlyout;

                    bResult = cmdBox1.AddCommands(cmdIDs, TextType);

                    cmdTab.AddSeparator(cmdBox1, cmdIDs[0]);

                }

            }
            thisAssembly = null;

        }

        public void RemoveCommandMgr()
        {
            iBmp.Dispose();

            iCmdMgr.RemoveCommandGroup(mainCmdGroupID);
            iCmdMgr.RemoveFlyoutGroup(flyoutGroupID);
        }

        public bool CompareIDs(int[] storedIDs, int[] addinIDs)
        {
            List<int> storedList = new List<int>(storedIDs);
            List<int> addinList = new List<int>(addinIDs);

            addinList.Sort();
            storedList.Sort();

            if (addinList.Count != storedList.Count)
            {
                return false;
            }
            else
            {

                for (int i = 0; i < addinList.Count; i++)
                {
                    if (addinList[i] != storedList[i])
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        public Boolean AddPMP()
        {
            ppage = new UserPMPage(this);
            return true;
        }

        public Boolean RemovePMP()
        {
            ppage = null;
            return true;
        }

        #endregion

        #region UI Callbacks
        public void CreateCube()
        {
            //make sure we have a part open
            string partTemplate = iSwApp.GetUserPreferenceStringValue((int)swUserPreferenceStringValue_e.swDefaultTemplatePart);
            if ((partTemplate != null) && (partTemplate != ""))
            {
                IModelDoc2 modDoc = (IModelDoc2)iSwApp.NewDocument(partTemplate, (int)swDwgPaperSizes_e.swDwgPaperA2size, 0.0, 0.0);

                modDoc.InsertSketch2(true);
                modDoc.SketchRectangle(0, 0, 0, .1, .1, .1, false);
                //Extrude the sketch
                IFeatureManager featMan = modDoc.FeatureManager;
                featMan.FeatureExtrusion(true,
                    false, false,
                    (int)swEndConditions_e.swEndCondBlind, (int)swEndConditions_e.swEndCondBlind,
                    0.1, 0.0,
                    false, false,
                    false, false,
                    0.0, 0.0,
                    false, false,
                    false, false,
                    true,
                    false, false);
            }
            else
            {
                System.Windows.Forms.MessageBox.Show("There is no part template available. Please check your options and make sure there is a part template selected, or select a new part template.");
            }
        }

        public void Print_Files_in_Folder()
        {
             
            ModelDoc2 swDoc = null;
            PartDoc swPart = null;
            DrawingDoc swDrawing = null;
            AssemblyDoc swAssembly = null;
            bool boolstatus = false;
            int longstatus = 0;
            int longwarnings = 0;
            string[] Files;
            string extension;
            string filename;

            FolderBrowserDialog fbd = new FolderBrowserDialog();
            DialogResult result = fbd.ShowDialog();

            if(result!=DialogResult.OK)
                return;
            

            Files = GetFiles(SolidworksFormats.Drawing,fbd.SelectedPath);
            if (Files != null)
            {
                for (int i = 0; i < Files.Length; i++)
                {
                    if (!Files[i].Contains("$"))
                    {
                        // PrintSheets(Files[i],True);

                        Debug.Print(Files[i]);
                    }
                }
            }
            
        }


        /// <summary>
        /// Prints the actual Sheet
        /// </summary>
        public void PrintactiveSheet()
        {
            DrawingDoc swDrawing = null;
            PageSetup setup = null;
            ModelDoc2 model = null;
            Sheet sheet = null;
            int longstatus = 0;
            int longwarnings = 0;
            int papersize = 0;
            int sheetnumber = 0;
            string[] sheetlist = null;
            double width = 0;
            double heigth = 0;

            model = iSwApp.ActiveDoc;
            if (model.GetType() == (int)swDocumentTypes_e.swDocDRAWING)
            {
                swDrawing = (DrawingDoc)model;
                setup = model.PageSetup;
                
                sheet = swDrawing.GetCurrentSheet();
                sheetlist = (string[])swDrawing.GetSheetNames();  
                papersize = sheet.GetSize(ref width, ref heigth);
               // Debug.Print("ID {0}",sheet.GetID());

                for (int i = 1; i < sheetlist.Length + 1; i++)
                {
                    sheetnumber = i;
                    if (sheetlist[i-1] == sheet.GetName())
                        break;
                }


                switch (papersize)
                {
                    case (int)swDwgPaperSizes_e.swDwgPaperA4size:
                        Debug.Print("A4");
                        setup.PrinterPaperSize = (int)swDwgPaperSizes_e.swDwgPaperA4size;
                        setup.Orientation = (int)swPageSetupOrientation_e.swPageSetupOrient_Landscape; //Landscape
                       // model.Extension.PrintOut(sheetnumber, sheetnumber, 1, true, "", "");
                        break;
                    case (int)swDwgPaperSizes_e.swDwgPaperA4sizeVertical:
                        Debug.Print("A4 vertical");
                        setup.PrinterPaperSize = (int)swDwgPaperSizes_e.swDwgPaperA4sizeVertical;
                        setup.Orientation = (int)swPageSetupOrientation_e.swPageSetupOrient_Portrait; //Portrait
                       // model.Extension.PrintOut(sheetnumber, sheetnumber, 1, true, "", "");
                        break;
                }

            }
        }

        public void PrintActiveDocument()
        {
            DrawingDoc swDrawing = null;
            PageSetup setup = null;
            ModelDoc2 model = null;

            model = iSwApp.ActiveDoc;
            if(model.GetType() == (int)swDocumentTypes_e.swDocDRAWING)
            {
            PrintSheets(model.GetTitle(),false);
            }
        }
        /// <summary>
        /// Print Sheets in a Drawing
        /// </summary>
        /// <param name="filename"></param>
        public void PrintSheets(string filename,bool Closefiles)
        {
            DrawingDoc swDrawing = null;
            PageSetup setup = null;
            ModelDoc2 model = null;
            string title = "";
           // PrintSpecification printspec = null;
            int longstatus = 0;
            int longwarnings = 0;
            int papersize = 0;
            string[] sheetlist = null;
            double width = 0;
            double heigth = 0;

            try
            {
                
                
                model = ((ModelDoc2)(iSwApp.OpenDoc6(filename,(int)swDocumentTypes_e.swDocDRAWING, (int)swOpenDocOptions_e.swOpenDocOptions_Silent, "", ref longstatus, ref longwarnings)));
                if (model.GetType() == (int)swDocumentTypes_e.swDocDRAWING)
                {
                    swDrawing = (DrawingDoc)model;
                    title = model.GetTitle();
                    
                }
                else
                {
                    return;
                }
                //System.Windows.Forms.MessageBox.Show("");
                //swDwgPaperSizes_e.swDwgPaperA4sizeVertical
                sheetlist = (string[])swDrawing.GetSheetNames();
                for (int i = 1; i < sheetlist.Length + 1;i++ )
                {
                    
                    setup = model.PageSetup;
                   // Debug.Print("Size: {0} , Orientation : {1}", setup.PrinterPaperSize, setup.Orientation);
                   // Debug.Print(swDrawing.Sheet[sheetlist[i]].GetName());
                    //Debug.Print(swDrawing.Sheet[sheetlist[i]].GetSheetFormatName());
                   // Debug.Print(swDrawing.Sheet[sheetlist[i]].GetSheetFormatName());
                    papersize = swDrawing.Sheet[sheetlist[i-1]].GetSize(ref width, ref heigth);
                     
                   // Debug.Print(string.Format("{0} x {1}", width, heigth));
                   // model.Extension.UsePageSetup = swPageSetupinuse
                  //  model.Extension.UsePageSetup = setup;
                  //  model.Extension.PrintOut(i, i, 1, true, "", "");
                    //swDrawing.Sheet[sh.GetName()].GetSheetFormatName();
                    switch (papersize)
                    {
                        case (int)swDwgPaperSizes_e.swDwgPaperA4size :
                            Debug.Print("A4");
                            setup.PrinterPaperSize = (int)swDwgPaperSizes_e.swDwgPaperA4size;
                            setup.Orientation = (int)swPageSetupOrientation_e.swPageSetupOrient_Landscape; //Landscape
                         //   model.Extension.PrintOut(i,i,1,true,"","");
                            break;
                        case (int)swDwgPaperSizes_e.swDwgPaperA4sizeVertical:
                            Debug.Print("A4 vertical");
                            setup.PrinterPaperSize = (int)swDwgPaperSizes_e.swDwgPaperA4sizeVertical;
                            setup.Orientation = (int)swPageSetupOrientation_e.swPageSetupOrient_Portrait; //Portrait
                        //    model.Extension.PrintOut(i, i, 1, true, "", "");
                            break;
                    }

                }
                if (Closefiles)
                {
                    iSwApp.CloseDoc(title);
                }
            }
            catch (Exception ex)
            {

              System.Windows.Forms.MessageBox.Show(ex.Message);
            }
            finally
            {
                sheetlist = null;
                swDrawing = null;
            }
        }

        public void SaveEDrawings()
        {
            int longstatus;
           // longstatus = swDoc.SaveAs3(Files[i].Substring(0, Files[i].Length - extension.Length) + EDrawingFormats.Drawing, 0, 0);
        }

        public string[] GetFiles(string Extensions, string Folder)
        {
            string[] files;
            string path = Folder.ToString();
            if(Directory.Exists(path))
            {
               // System.Windows.Forms.MessageBox.Show("Test Test");
                files = Directory.GetFiles(path, "*" + Extensions, SearchOption.AllDirectories);
            if (files.Length == 0)
                return null;
            return files;
            }
            else
            {//
                System.Windows.Forms.MessageBox.Show("Directory not found");
                return null;
            }
        }

        public class SolidworksFormats
        {
            public const string Part = ".SLDPRT";
            public const string Assembly = ".SLDASM";
            public const string Drawing = ".SLDDRW";
            public const string Library = ".SLDLFP";
        }

        public class EDrawingFormats
        {
            public const string Part = ".EPRT";
            public const string Assembly = ".EASM";
            public const string Drawing = ".EDRW";
       
        }

        public void ShowPMP()
        {
            if (ppage != null)
                ppage.Show();
        }

        public int EnablePMP()
        {
            if (iSwApp.ActiveDoc != null)
                return 1;
            else
                return 0;
        }

        public void FlyoutCallback()
        {
            FlyoutGroup flyGroup = iCmdMgr.GetFlyoutGroup(flyoutGroupID);
            flyGroup.RemoveAllCommandItems();

            flyGroup.AddCommandItem(System.DateTime.Now.ToLongTimeString(), "test", 0, "FlyoutCommandItem1", "FlyoutEnableCommandItem1");

        }
        public int FlyoutEnable()
        {
            return 1;
        }

        public void FlyoutCommandItem1()
        {
            iSwApp.SendMsgToUser("Flyout command 1");
        }

        public int FlyoutEnableCommandItem1()
        {
            return 1;
        }
        #endregion

        #region Event Methods
        public bool AttachEventHandlers()
        {
            AttachSwEvents();
            //Listen for events on all currently open docs
            AttachEventsToAllDocuments();
            return true;
        }

        private bool AttachSwEvents()
        {
            try
            {
                SwEventPtr.ActiveDocChangeNotify += new DSldWorksEvents_ActiveDocChangeNotifyEventHandler(OnDocChange);
                SwEventPtr.DocumentLoadNotify2 += new DSldWorksEvents_DocumentLoadNotify2EventHandler(OnDocLoad);
                SwEventPtr.FileNewNotify2 += new DSldWorksEvents_FileNewNotify2EventHandler(OnFileNew);
                SwEventPtr.ActiveModelDocChangeNotify += new DSldWorksEvents_ActiveModelDocChangeNotifyEventHandler(OnModelChange);
                SwEventPtr.FileOpenPostNotify += new DSldWorksEvents_FileOpenPostNotifyEventHandler(FileOpenPostNotify);
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return false;
            }
        }



        private bool DetachSwEvents()
        {
            try
            {
                SwEventPtr.ActiveDocChangeNotify -= new DSldWorksEvents_ActiveDocChangeNotifyEventHandler(OnDocChange);
                SwEventPtr.DocumentLoadNotify2 -= new DSldWorksEvents_DocumentLoadNotify2EventHandler(OnDocLoad);
                SwEventPtr.FileNewNotify2 -= new DSldWorksEvents_FileNewNotify2EventHandler(OnFileNew);
                SwEventPtr.ActiveModelDocChangeNotify -= new DSldWorksEvents_ActiveModelDocChangeNotifyEventHandler(OnModelChange);
                SwEventPtr.FileOpenPostNotify -= new DSldWorksEvents_FileOpenPostNotifyEventHandler(FileOpenPostNotify);
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return false;
            }

        }

        public void AttachEventsToAllDocuments()
        {
            ModelDoc2 modDoc = (ModelDoc2)iSwApp.GetFirstDocument();
            while (modDoc != null)
            {
                if (!openDocs.Contains(modDoc))
                {
                    AttachModelDocEventHandler(modDoc);
                }
                else if (openDocs.Contains(modDoc))
                {
                    bool connected = false;
                    DocumentEventHandler docHandler = (DocumentEventHandler)openDocs[modDoc];
                    if (docHandler != null)
                    {
                        connected = docHandler.ConnectModelViews();
                    }
                }

                modDoc = (ModelDoc2)modDoc.GetNext();
            }
        }

        public bool AttachModelDocEventHandler(ModelDoc2 modDoc)
        {
            if (modDoc == null)
                return false;

            DocumentEventHandler docHandler = null;

            if (!openDocs.Contains(modDoc))
            {
                switch (modDoc.GetType())
                {
                    case (int)swDocumentTypes_e.swDocPART:
                        {
                            docHandler = new PartEventHandler(modDoc, this);
                            break;
                        }
                    case (int)swDocumentTypes_e.swDocASSEMBLY:
                        {
                            docHandler = new AssemblyEventHandler(modDoc, this);
                            break;
                        }
                    case (int)swDocumentTypes_e.swDocDRAWING:
                        {
                            docHandler = new DrawingEventHandler(modDoc, this);
                            break;
                        }
                    default:
                        {
                            return false; //Unsupported document type
                        }
                }
                docHandler.AttachEventHandlers();
                openDocs.Add(modDoc, docHandler);
            }
            return true;
        }

        public bool DetachModelEventHandler(ModelDoc2 modDoc)
        {
            DocumentEventHandler docHandler;
            docHandler = (DocumentEventHandler)openDocs[modDoc];
            openDocs.Remove(modDoc);
            modDoc = null;
            docHandler = null;
            return true;
        }

        public bool DetachEventHandlers()
        {
            DetachSwEvents();

            //Close events on all currently open docs
            DocumentEventHandler docHandler;
            int numKeys = openDocs.Count;
            object[] keys = new Object[numKeys];

            //Remove all document event handlers
            openDocs.Keys.CopyTo(keys, 0);
            foreach (ModelDoc2 key in keys)
            {
                docHandler = (DocumentEventHandler)openDocs[key];
                docHandler.DetachEventHandlers(); //This also removes the pair from the hash
                docHandler = null;
            }
            return true;
        }
        #endregion

        #region Event Handlers
        //Events
        public int OnDocChange()
        {
            return 0;
        }

        public int OnDocLoad(string docTitle, string docPath)
        {
            return 0;
        }

        int FileOpenPostNotify(string FileName)
        {
            AttachEventsToAllDocuments();
            return 0;
        }

        public int OnFileNew(object newDoc, int docType, string templateName)
        {
            AttachEventsToAllDocuments();
            return 0;
        }

        public int OnModelChange()
        {
            return 0;
        }

        #endregion
    }

}
