﻿using KSPDev.ConfigUtils;
using KSPDev.GUIUtils;
using KSPDev.LogUtils;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections;
using System.Text;
using UnityEngine;

using Logger = KSPDev.LogUtils.Logger;

namespace KIS {

[PersistentFieldsDatabase("KIS/settings/KISConfig")]
public class ModuleKISInventory : PartModule, IPartCostModifier, IPartMassModifier {
  // Inventory
  public Dictionary<int, KIS_Item> items = new Dictionary<int, KIS_Item>();
  [KSPField]
  public bool externalAccess = true;
  [KSPField]
  public bool internalAccess = true;
  [KSPField]
  public int slotsX = 6;
  [KSPField]
  public int slotsY = 4;
  [KSPField]
  public int slotSize = 50;
  [KSPField]
  public int itemIconResolution = 128;
  [KSPField]
  public int selfIconResolution = 128;
  [KSPField]
  public float maxVolume = 1;
  [KSPField]
  public string openSndPath = "KIS/Sounds/containerOpen";
  [KSPField]
  public string closeSndPath = "KIS/Sounds/containerClose";
  [KSPField]
  public string helmetOnSndPath = "KIS/Sounds/helmetOn";
  [KSPField]
  public string helmetOffSndPath = "KIS/Sounds/helmetOff";
  [KSPField]
  public string defaultMoveSndPath = "KIS/Sounds/itemMove";
  [KSPField(isPersistant = true)]
  public string invName = "";
  [KSPField(isPersistant = true)]
  public bool helmetEquipped = true;

  [PersistentField("EvaInventory/inventoryKey")]
  public static string evaInventoryKey = "tab";

  [PersistentField("EvaInventory/rightHandKey")]
  public static string evaRightHandKey = "x";

  [PersistentField("EvaInventory/helmetKey")]
  public static string evaHelmetKey = "j";
      
  // Inventory hotkeys control.
  [PersistentField("Global/slotHotkeysEnabled")]
  public static bool inventoryKeysEnabled = true;

  [PersistentField("Global/slotHotkey1")]
  public static KeyCode slotHotkey1 = KeyCode.Alpha1;

  [PersistentField("Global/slotHotkey2")]
  public static KeyCode slotHotkey2 = KeyCode.Alpha2;

  [PersistentField("Global/slotHotkey3")]
  public static KeyCode slotHotkey3 = KeyCode.Alpha3;

  [PersistentField("Global/slotHotkey4")]
  public static KeyCode slotHotkey4 = KeyCode.Alpha4;

  [PersistentField("Global/slotHotkey5")]
  public static KeyCode slotHotkey5 = KeyCode.Alpha5;

  [PersistentField("Global/slotHotkey6")]
  public static KeyCode slotHotkey6 = KeyCode.Alpha6;

  [PersistentField("Global/slotHotkey7")]
  public static KeyCode slotHotkey7 = KeyCode.Alpha7;

  [PersistentField("Global/slotHotkey8")]
  public static KeyCode slotHotkey8 = KeyCode.Alpha8;

  [PersistentField("Global/kerbalDefaultMass")]
  public static float kerbalDefaultMass = 0.094f;

  [PersistentField("Global/itemDebug")]
  public static bool debugContextMenu = false;

  [PersistentField("Editor/PodInventory/addToAllSeats", isCollection = true)]
  public static List<String> defaultItemsForAllSeats = new List<string>();

  [PersistentField("Editor/PodInventory/addToTheFirstSeatOnly", isCollection = true)]
  public static List<String> defaultItemsForTheFirstSeat = new List<string>();

  // UI strings.
  const string strMaxVolumeReached =
      "Max destination volume reached. Part volume is: {0:#.####}L (+{1:#.####}L)";

  public string openGuiName;
  public float totalVolume = 0;
  public int podSeat = -1;
  public InventoryType invType = InventoryType.Container;
  public enum InventoryType {
    Container,
    Pod,
    Eva
  }
  float keyPressTime = 0f;
  public delegate void DelayedActionMethod(KIS_Item item);
  public string kerbalTrait;
  List<KIS_Item> startEquip = new List<KIS_Item>();

  // GUI
  public bool showGui = false;
  GUIStyle lowerRightStyle, upperLeftStyle, upperRightStyle, buttonStyle, boxStyle;
  public Rect guiMainWindowPos;
  Rect guiDebugWindowPos = new Rect(0, 50, 500, 300);
  KIS_IconViewer icon = null;
  Rect defaultEditorPos = new Rect(Screen.width / 3, 40, 10, 10);
  Rect defaultFlightPos = new Rect(0, 50, 10, 10);
  Vector2 scrollPositionDbg;
  float splitQty = 1;
  bool clickThroughLocked = false;
  bool guiSetName = false;
  bool PartActionUICreated = false;

  //Tooltip
  private KIS_Item tooltipItem;

  // Context menu
  private KIS_Item contextItem;
  private bool contextClick = false;
  private int contextSlot;
  private Rect contextRect;

  // Animation (Not tested)
  [KSPField]
  string openAnimName = "Doors";
  [KSPField]
  float openAnimSpeed = 1f;
  Animation openAnim;

  // Sounds
  public FXGroup sndFx;

  // Debug
  private KIS_Item debugItem;
  
  // Messages.  
  const string NoItemEquippedMsg = "Cannot use equipped item because nothing is equipped";

  /// <summary>Overridden from PartModule.</summary>
  public override string GetInfo() {
    var sb = new StringBuilder();
    sb.AppendFormat("<b>Max Volume</b>: {0:F2} L", maxVolume);
    sb.AppendLine();
    sb.AppendFormat("<b>Internal access</b>: {0}", internalAccess ? "ALLOWED" : "DISALLOWED");
    sb.AppendLine();
    sb.AppendFormat("<b>External access</b>: {0}", externalAccess ? "ALLOWED" : "DISALLOWED");
    sb.AppendLine();
    return sb.ToString();
  }

  /// <summary>Overridden from PartModule.</summary>
  public override void OnStart(StartState state) {
    base.OnStart(state);
    if (state == StartState.None) {
      return;
    }
    if ((state & StartState.Editor) == 0) {
      // Clean pod's inventory if the seat is unoccupied.
      if (invType == InventoryType.Pod && podSeat != -1) {
        var crewAtPodSeat = part.protoModuleCrew.Find(x => x.seatIdx == podSeat);
        if (crewAtPodSeat == null && items.Count > 0) {
          Logger.logInfo("Clear unoccupied seat inventory: pod={0}, seat={1}, count={2}, mass={3}",
                         part, podSeat, items.Count, GetContentMass());
          items.Clear();
          RefreshMassAndVolume();
        }
      }
    }

    if (HighLogic.LoadedSceneIsEditor) {
      InputLockManager.RemoveControlLock("KISInventoryLock");
      GameEvents.onTooltipDestroyRequested.Add(OnTooltipDestroyRequestedEvent);
    }
    guiMainWindowPos = defaultFlightPos;

    Animation[] anim = this.part.FindModelAnimators(openAnimName);
    if (anim.Length > 0) {
      openAnim = this.part.FindModelAnimators(openAnimName)[0];
    }

    GameEvents.onCrewTransferred.Add(OnCrewTransferred);
    GameEvents.onVesselChange.Add(OnVesselChange);
    GameEvents.onPartActionUICreate.Add(OnPartActionUICreate);
    GameEvents.onPartActionUIDismiss.Add(OnPartActionUIDismiss);
          
    if (invType == InventoryType.Eva) {
      List<ProtoCrewMember> protoCrewMembers = vessel.GetVesselCrew();
      kerbalTrait = protoCrewMembers[0].experienceTrait.Title;
    }
    sndFx.audio = part.gameObject.AddComponent<AudioSource>();
    sndFx.audio.volume = GameSettings.SHIP_VOLUME;
    sndFx.audio.rolloffMode = AudioRolloffMode.Linear;
    sndFx.audio.dopplerLevel = 0f;
    sndFx.audio.spatialBlend = 1f;
    sndFx.audio.maxDistance = 10;
    sndFx.audio.loop = false;
    sndFx.audio.playOnAwake = false;
    foreach (var item in startEquip) {
      Logger.logInfo("equip {0}", item.availablePart.name);
      item.Equip();
    }
    RefreshMassAndVolume();

    if (!helmetEquipped) {
      SetHelmet(false, true);
    }
  }

  /// <summary>Overridden from MonoBehavior.</summary>
  void Update() {
    if (showGui) {
      if (HighLogic.LoadedSceneIsFlight) {
        if (FlightGlobals.ActiveVessel.isEVA) {
          float distEvaToContainer = Vector3.Distance(FlightGlobals.ActiveVessel.transform.position,
                                                      this.part.transform.position);
          ModuleKISPickup mPickup = KISAddonPickup.instance.GetActivePickupNearest(this.part);
          if (!mPickup || distEvaToContainer > mPickup.maxDistance) {
            ShowInventory();
          }
        }
      }
    }
    UpdateKey();
  }

  /// <summary>Overridden from MonoBehavior.</summary>
  void LateUpdate() {
    foreach (KeyValuePair<int, KIS_Item> item in items) {
      item.Value.Update();
    }
  }

  void UpdateKey() {
    if (!HighLogic.LoadedSceneIsFlight
        || FlightGlobals.ActiveVessel != this.part.vessel
        || !FlightGlobals.ActiveVessel.isEVA) {
      return;
    }

    // Open inventory on keypress
    if (Input.GetKeyDown(evaInventoryKey.ToLower())) {
      ShowInventory();
    }
    // Use slot when not in drag mode.
    if (!KISAddonPointer.isRunning) {
      slotKeyPress(slotHotkey1, 0, 1);
      slotKeyPress(slotHotkey2, 1, 1);
      slotKeyPress(slotHotkey3, 2, 1);
      slotKeyPress(slotHotkey4, 3, 1);
      slotKeyPress(slotHotkey5, 4, 1);
      slotKeyPress(slotHotkey6, 5, 1);
      slotKeyPress(slotHotkey7, 6, 1);
      slotKeyPress(slotHotkey8, 7, 1);
    }

    // Use right hand tool
    if (Input.GetKeyDown(evaRightHandKey)) {
      KIS_Item rightHandItem = GetEquipedItem("rightHand");
      if (rightHandItem != null) {
        rightHandItem.Use(KIS_Item.UseFrom.KeyDown);
      } else {
        KIS_UISoundPlayer.instance.PlayBipWrong();
        ScreenMessaging.ShowInfoScreenMessage(NoItemEquippedMsg);
      }
    }

    if (Input.GetKeyUp(evaRightHandKey)) {
      KIS_Item rightHandItem = GetEquipedItem("rightHand");
      if (rightHandItem != null) {
        rightHandItem.Use(KIS_Item.UseFrom.KeyUp);
      }
    }

    // Put/remove helmet
    if (Input.GetKeyDown(evaHelmetKey)) {
      if (helmetEquipped) {
        if (SetHelmet(false, true)) {
          PlaySound(helmetOffSndPath);
        }
      } else {
        if (SetHelmet(true)) {
          PlaySound(helmetOnSndPath);
        }
      }
    }
  }

  /// <summary>Overridden from PartModule.</summary>
  public override void OnLoad(ConfigNode node) {
    base.OnLoad(node);
    foreach (ConfigNode cn in node.nodes) {
      if (cn.name == "ITEM") {
        if (cn.HasValue("partName") && cn.HasValue("slot") && cn.HasValue("quantity")) {
          string availablePartName = cn.GetValue("partName");
          AvailablePart availablePart = PartLoader.getPartInfoByName(availablePartName);
          if (availablePart != null) {
            int slot = int.Parse(cn.GetValue("slot"));
            int qty = int.Parse(cn.GetValue("quantity"));
            KIS_Item item = null;
            if (cn.HasNode("PART")) {
              item = AddItem(availablePart, cn, qty, slot);
            } else {
              Logger.logWarning("No part node found on item {0}, creating new one from prefab",
                                availablePartName);
              item = AddItem(availablePart.partPrefab, qty, slot);
            }
            if (cn.HasValue("equipped") && item != null
                && bool.Parse(cn.GetValue("equipped")) && this.invType == InventoryType.Eva) {
              startEquip.Add(item);
            }
          } else {
            Logger.logError("Unable to load {0} from inventory", availablePartName);
          }
        } else {
          Logger.logError("Unable to load an item from inventory");
        }
      }
    }
  }

  /// <summary>Overridden from PartModule.</summary>
  public override void OnSave(ConfigNode node) {
    base.OnSave(node);
    foreach (KeyValuePair<int, KIS_Item> item in items) {
      ConfigNode itemNode = node.AddNode("ITEM");
      item.Value.OnSave(itemNode);

      // Science recovery works by retrieving all MODULE/ScienceData 
      // subnodes from the part node, so copy all experiments from 
      // contained parts to where it expects to find them. 
      // This duplicates data but allows recovery to work properly. 
      foreach (ConfigNode module in item.Value.partNode.GetNodes("MODULE")) {
        foreach (ConfigNode experiment in module.GetNodes("ScienceData")) {
          experiment.CopyTo(node.AddNode("ScienceData"));
        }
      }
    }
  }

  /// <summary>Plays a sound at the inventory's location.</summary>
  /// <param name="sndPath">Path to the sound clip.</param>
  /// <param name="loop">Indefinitely repeat sound when <c>true</c>.</param>
  /// <param name="uiSnd">If <c>true</c> then sound is 2D (i.e. not bound to the inventory 3D
  /// position).</param>
  public void PlaySound(string sndPath, bool loop = false, bool uiSnd = true) {
    if (GameDatabase.Instance.ExistsAudioClip(sndPath)) {
      sndFx.audio.clip = GameDatabase.Instance.GetAudioClip(sndPath);
      sndFx.audio.loop = loop;
      if (uiSnd) {
        sndFx.audio.volume = GameSettings.UI_VOLUME;
        sndFx.audio.spatialBlend = 0;  //set as 2D audiosource
      } else {
        sndFx.audio.volume = GameSettings.SHIP_VOLUME;
        sndFx.audio.spatialBlend = 1;  //set as 3D audiosource
      }
    } else {
      Logger.logError("Sound not found in the game database !");
      ScreenMessaging.ShowPriorityScreenMessageWithTimeout(
          10, "Sound file : {0} has not been found, please check installation path !", sndPath);
    }
    sndFx.audio.Play();
  }

  /// <summary>Helper method to get all inventories on the part with open UI.</summary>
  /// <returns>List of inventories.</returns>
  public static List<ModuleKISInventory> GetAllOpenInventories() {
    var openInventories = new List<ModuleKISInventory>();
    var allInventory = FindObjectsOfType(typeof(ModuleKISInventory)) as ModuleKISInventory[];
    foreach (var inventory in allInventory) {
      if (inventory.showGui) {
        openInventories.Add(inventory);
      }
    }
    return openInventories;
  }

  void OnVesselChange(Vessel vess) {
    if (showGui) {
      ShowInventory();
    }
  }

  void OnCrewTransferred(GameEvents.HostedFromToAction<ProtoCrewMember, Part> fromToAction) {
    if (fromToAction.from == part && invType == InventoryType.Pod) {
      if (fromToAction.to.vessel.isEVA) {
        // pod to eva
        ProtoCrewMember crewAtPodSeat =
            fromToAction.from.protoModuleCrew.Find(x => x.seatIdx == podSeat);
        if (items.Count > 0 && crewAtPodSeat == null) {
          ModuleKISInventory destInventory = fromToAction.to.GetComponent<ModuleKISInventory>();
          Logger.logInfo("Items transfer | source {0} ({1})", part.name, podSeat);
          Logger.logInfo("Items transfer | destination: {0}", destInventory.part.name);
          MoveItems(items, destInventory);
          RefreshMassAndVolume();
          destInventory.RefreshMassAndVolume();
        }
      } else {
        // pod to pod

        // Workaround to set a seat index on pod without internal (because KSP don't do it for an unknow reason)
        if (fromToAction.host.seatIdx == -1) {
          Logger.logWarning("protoCrew seatIdx is set to -1 ! (no internal ?)");
          fromToAction.host.seatIdx = GetFirstFreeSeatIdx(fromToAction.to);
          Logger.logInfo("Setting seat to: {0}", fromToAction.host.seatIdx);
        }

        ProtoCrewMember crewAtPodSeat =
            fromToAction.from.protoModuleCrew.Find(x => x.seatIdx == podSeat);
        if (items.Count > 0 && crewAtPodSeat == null) {
          Logger.logInfo("Items transfer | source: {0} ({1})", part.name, podSeat);
          // Find target seat and schedule a coroutine.
          var destInventory = fromToAction.to.GetComponents<ModuleKISInventory>().ToList()
              .Find(x => x.podSeat == fromToAction.host.seatIdx);
          StartCoroutine(destInventory.WaitAndTransferItems(this.items, fromToAction.host, this));
        }
      }
    }

    if (fromToAction.to == this.part && invType == InventoryType.Pod) {
      if (fromToAction.from.vessel.isEVA) {
        // eva to pod

        // Workaround to set a seat index on pod without internal (because KSP don't do it for an unknow reason)
        if (fromToAction.host.seatIdx == -1) {
          Logger.logWarning("protoCrew seatIdx has been set to -1 ! (no internal ?)");
          fromToAction.host.seatIdx = GetFirstFreeSeatIdx(fromToAction.to);
          Logger.logInfo("Setting seat to: {0}", fromToAction.host.seatIdx);
          if (fromToAction.host.seatIdx == -1) {
            Logger.logError("A seat must be available!");
          }
        }

        ModuleKISInventory evaInventory = fromToAction.from.GetComponent<ModuleKISInventory>();
        Logger.logInfo("Item transfer | source {0}", fromToAction.host.name);
        List<KIS_Item> itemsToDrop = new List<KIS_Item>();
        foreach (KeyValuePair<int, KIS_Item> item in evaInventory.items) {
          if (item.Value.carriable) {
            itemsToDrop.Add(item.Value);
          } else if (item.Value.equipped) {
            item.Value.Unequip();
          }
        }
        foreach (KIS_Item item in itemsToDrop) {
          item.Drop(part);
        }
        var transferedItems = new Dictionary<int, KIS_Item>(evaInventory.items);
        StartCoroutine(WaitAndTransferItems(transferedItems, fromToAction.host));
      }
    }
  }

  int GetFirstFreeSeatIdx(Part p) {
    for (int i = 0; i <= (p.protoModuleCrew.Count + 1); i++) {
      ProtoCrewMember pcm = p.protoModuleCrew.Find(x => x.seatIdx == i);
      if (pcm == null) {
        return i;
      }
    }
    Logger.logError("Cannot find a free seat in: {0}", p.name);
    return -1;
  }

  IEnumerator WaitAndTransferItems(Dictionary<int, KIS_Item> transferedItems,
                                   ProtoCrewMember protoCrew,
                                   ModuleKISInventory srcInventory = null) {
    yield return new WaitForFixedUpdate();
    ProtoCrewMember crewAtPodSeat = this.part.protoModuleCrew.Find(x => x.seatIdx == podSeat);
    if (crewAtPodSeat == protoCrew) {
      MoveItems(transferedItems, this);
      Logger.logInfo("Item transfer | destination: {0} ({1})", part.name, podSeat);
      this.RefreshMassAndVolume();
      if (srcInventory) {
        srcInventory.RefreshMassAndVolume();
      }
    }
  }

  void OnPartActionUICreate(Part p) {
    if (part != p || PartActionUICreated) {
      return;
    }
    // Update context menu
    if (invType == InventoryType.Pod) {
      if (HighLogic.LoadedSceneIsEditor) {
        Events["ShowInventory"].guiActive = true;
        Events["ShowInventory"].guiActiveUnfocused = true;
        Events["ShowInventory"].guiName = "Seat " + podSeat + " inventory";
      } else {
        Events["ShowInventory"].guiActive = false;
        Events["ShowInventory"].guiActiveUnfocused = false;
        ProtoCrewMember crewAtPodSeat = this.part.protoModuleCrew.Find(x => x.seatIdx == podSeat);
        if (crewAtPodSeat != null) {
          string kerbalName = crewAtPodSeat.name.Split(' ').FirstOrDefault();
          Events["ShowInventory"].guiActive = true;
          Events["ShowInventory"].guiActiveUnfocused = true;
          Events["ShowInventory"].guiName = kerbalName + "'s inventory";
        }
      }
    } else {
      Events["ShowInventory"].guiActive = true;
      Events["ShowInventory"].guiActiveUnfocused = true;
      Events["ShowInventory"].guiName = "Inventory";
      if (invName != "") {
        Events["ShowInventory"].guiName = "Inventory | " + invName;
      }
    }
    if (HighLogic.LoadedSceneIsFlight) {
      ModuleKISPickup mPickup = KISAddonPickup.instance.GetActivePickupNearest(part);
      if (mPickup) {
        Events["ShowInventory"].unfocusedRange = mPickup.maxDistance;
      }
    }
    PartActionUICreated = true;
  }

  void OnPartActionUIDismiss(Part p) {
    if (part == p) {
      PartActionUICreated = false;
    }
  }

  /// <summary>Refreshes container mass, volume and cost.</summary>
  public void RefreshMassAndVolume() {
    // Update volume.
    totalVolume = GetContentVolume();
    // Update vessel cost in editor.
    if (HighLogic.LoadedSceneIsEditor) {
      GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
    }
  }

  /// <summary>Overridden from MonoBehavior.</summary>
  void OnDestroy() {
    GameEvents.onCrewTransferred.Remove(new EventData<GameEvents.HostedFromToAction<ProtoCrewMember,
                                        Part>>.OnEvent(this.OnCrewTransferred));
    GameEvents.onVesselChange.Remove(new EventData<Vessel>.OnEvent(this.OnVesselChange));
    GameEvents.onPartActionUICreate.Remove(new EventData<Part>.OnEvent(this.OnPartActionUICreate));
    GameEvents.onPartActionUIDismiss.Remove(
        new EventData<Part>.OnEvent(this.OnPartActionUIDismiss));
    if (HighLogic.LoadedSceneIsEditor) {
      InputLockManager.RemoveControlLock("KISInventoryLock");
      GameEvents.onTooltipDestroyRequested.Remove(OnTooltipDestroyRequestedEvent);
    }
  }

  void slotKeyPress(KeyCode kc, int slot, int delay = 1) {
    if (kc == KeyCode.None || !inventoryKeysEnabled) {
      return;
    }

    // TODO: Add a check for shift keys to not trigger use action on combinations with
    // Shift, Ctrl, and Alt.
    if (Input.GetKeyDown(kc)) {
      keyPressTime = Time.time;
    }
    if (Input.GetKey(kc)) {
      if (Time.time - keyPressTime >= delay) {
        if (items.ContainsKey(slot)) {
          items[slot].Use(KIS_Item.UseFrom.InventoryShortcut);
        }
        keyPressTime = Mathf.Infinity;
      }
    }
    if (Input.GetKeyUp(kc)) {
      if (keyPressTime != Mathf.Infinity && items.ContainsKey(slot)) {
        items[slot].ShortcutKeyPress();
      }
      keyPressTime = 0;
    }
  }

  public virtual KIS_Item AddItem(AvailablePart availablePart, ConfigNode partNode,
                          float qty = 1, int slot = -1) {
    KIS_Item item = null;
    if (items.ContainsKey(slot)) {
      slot = -1;
    }
    int maxSlot = (slotsX * slotsY) - 1;
    if (slot < 0 || slot > maxSlot) {
      slot = GetFreeSlot();
      if (slot == -1) {
        Logger.logError("AddItem error : No free slot available for {0}", availablePart.title);
        return null;
      }
    }
    item = new KIS_Item(availablePart, partNode, this, qty);
    items.Add(slot, item);
    if (showGui) {
      items[slot].EnableIcon(itemIconResolution);
    }
    RefreshMassAndVolume();
    return item;
  }

  public virtual KIS_Item AddItem(Part part, float qty = 1, int slot = -1) {
    KIS_Item item = null;
    if (items.ContainsKey(slot)) {
      slot = -1;
    }
    int maxSlot = (slotsX * slotsY) - 1;
    if (slot < 0 || slot > maxSlot) {
      slot = GetFreeSlot();
      if (slot == -1) {
        Logger.logError("AddItem error : No free slot available for {0}", part.partInfo.title);
        return null;
      }
    }
    item = new KIS_Item(part, this, qty);
    items.Add(slot, item);
    if (showGui) {
      items[slot].EnableIcon(itemIconResolution);
    }
    RefreshMassAndVolume();
    return item;
  }

  public virtual void AddItem (KIS_Item item, int slot) {
    int srcSlot = item.slot;
    items.Add (slot, item);

    ModuleKISInventory srcInventory = item.inventory;
    srcInventory.items.Remove (srcSlot);
    srcInventory.RefreshMassAndVolume ();

    item.inventory = this;

    RefreshMassAndVolume ();
  }

  public static void MoveItem(KIS_Item srcItem, ModuleKISInventory tgtInventory, int tgtSlot) {
    srcItem.OnMove(srcItem.inventory, tgtInventory);
    tgtInventory.AddItem (srcItem, tgtSlot);
  }

  public static void MoveItems(Dictionary<int, KIS_Item> srcItems, ModuleKISInventory destInventory) {
    destInventory.items.Clear();
    destInventory.items = new Dictionary<int, KIS_Item>(srcItems);
    foreach (KeyValuePair<int, KIS_Item> item in destInventory.items) {
      item.Value.inventory = destInventory;
    }
    srcItems.Clear();
    srcItems = null;
  }

  public virtual bool DeleteItem(int slot) {
    if (items.ContainsKey(slot)) {
      return items[slot].StackRemove();
    }
    return true;
  }

  public virtual bool isFull() {
    return GetFreeSlot() < 0;
  }

  public KIS_Item GetEquipedItem(string equipSlot) {
    foreach (KeyValuePair<int, KIS_Item> item in this.items) {
      if (item.Value.equipped && item.Value.equipSlot == equipSlot) {
        return item.Value;
      }
    }
    return null;
  }

  public int GetFreeSlot() {
    int maxSlot = (slotsX * slotsY) - 1;
    for (int i = 0; i <= maxSlot; i++) {
      if (items.ContainsKey(i) == false) {
        return i;
      }
    }
    return -1;
  }

  public virtual float GetContentMass() {
    float contentMass = 0;
    foreach (KeyValuePair<int, KIS_Item> item in items) {
      contentMass += item.Value.totalMass;
    }
    return contentMass;
  }

  public virtual float GetContentVolume() {
    float contentVolume = 0;
    foreach (KeyValuePair<int, KIS_Item> item in items) {
      if (item.Value.carriable && invType == InventoryType.Eva) {
        contentVolume += 0;
      } else {
        contentVolume += item.Value.stackVolume;
      }
    }
    return contentVolume;
  }

  public virtual float GetContentCost() {
    float contentCost = 0;
    foreach (KeyValuePair<int, KIS_Item> item in items) {
      contentCost += item.Value.totalCost;
    }
    return contentCost;
  }

  /// <summary>Overridden from IPartCostModifier.</summary>
  public ModifierChangeWhen GetModuleCostChangeWhen() {
    // TODO(ihsoft): Figure out what value is right.
    return ModifierChangeWhen.FIXED;
  }

  /// <summary>Overridden from IPartCostModifier.</summary>
  public float GetModuleCost(float defaultCost, ModifierStagingSituation sit) {
    return GetContentCost();
  }

  /// <summary>Overridden from IPartMassModifier.</summary>
  public ModifierChangeWhen GetModuleMassChangeWhen() {
    return ModifierChangeWhen.CONSTANTLY;
  }
      
  /// <summary>Overridden from IPartMassModifier.</summary>
  public float GetModuleMass(float defaultMass, ModifierStagingSituation sit) {
    return GetContentMass();
  }

  /// <summary>Checks if part has a child, and reports the problem.</summary>
  /// <param name="p">A part to check.</param>
  /// <returns><c>true</c> if it's OK to put the part into the inventory.</returns>
  bool VerifyIsNotAssembly(Part p) {
    if (!HighLogic.LoadedSceneIsEditor && KISAddonPickup.grabbedPartsCount > 1) {
      ScreenMessaging.ShowPriorityScreenMessage(
          "Cannot put a part with children into the inventory. There are {0} part(s) attached",
          KISAddonPickup.grabbedPartsCount - 1);
      return false;
    }
    return true;
  }

  public bool VolumeAvailableFor(Part p) {
    float partVolume = KIS_Shared.GetPartVolume(p.partInfo);
    var newTotalVolume = GetContentVolume() + partVolume;
    if (newTotalVolume > maxVolume) {
      ScreenMessaging.ShowPriorityScreenMessage(
          strMaxVolumeReached, partVolume, newTotalVolume - maxVolume);
      return false;
    }
    return true;
  }

  public bool VolumeAvailableFor(KIS_Item item) {
    RefreshMassAndVolume();
    if (KISAddonPickup.draggedItem.inventory == this) {
      return true;
    } else {
      float newTotalVolume = GetContentVolume() + item.stackVolume;
      if (newTotalVolume > maxVolume) {
        ScreenMessaging.ShowPriorityScreenMessage(
            strMaxVolumeReached, item.stackVolume, (newTotalVolume - maxVolume));
        return false;
      } else {
        return true;
      }
    }
  }

  // TODO(ihsoft): Move out of base inventory module.
  public void DelayedAction(DelayedActionMethod actionMethod, KIS_Item item, float delay) {
    StartCoroutine(WaitAndDoAction(actionMethod, item, delay));
  }

  IEnumerator WaitAndDoAction(DelayedActionMethod actionMethod, KIS_Item item, float delay) {
    yield return new WaitForSeconds(delay);
    actionMethod(item);
  }

  [KSPEvent(name = "ContextMenuShowInventory", guiActiveEditor = true, active = true,
            guiActive = true, guiActiveUnfocused = true, guiName = "")]
  public virtual void ShowInventory() {
    if (showGui) {
      if (openAnim) {
        openAnim[openAnimName].speed = -openAnimSpeed;
        openAnim.Play(openAnimName);
      }
      DisableIcon();
      showGui = false;
      if (HighLogic.LoadedSceneIsEditor) {
        PlaySound(closeSndPath);
      } else {
        PlaySound(closeSndPath, false, false);
      }
      clickThroughLocked = false;
      if (HighLogic.LoadedSceneIsFlight) {
        InputLockManager.RemoveControlLock("KISInventoryFlightLock");
      }
      if (HighLogic.LoadedSceneIsEditor) {
        InputLockManager.RemoveControlLock("KISInventoryEditorLock");
      }
    } else {
      // Check if inventory can be opened from interior/exterior
      if (HighLogic.LoadedSceneIsFlight) {
        // Don't allow access to the container being carried by a kerbal. Its state is
        // serialized in the kerbal's invenotry so, any changes will be reverted once
        // the container is dropped.
        // TODO: Find a way to update serialized state and remove this check (#89). 
        if (GetComponent<ModuleKISItemEvaTweaker>() && vessel.isEVA) {
          ScreenMessaging.ShowPriorityScreenMessage(
              "This storage is not accessible while carried !");
          return;
        }
        if (FlightGlobals.ActiveVessel.isEVA && !externalAccess) {
          ScreenMessaging.ShowPriorityScreenMessage(
              "This storage is not accessible from the outside !");
          return;
        }
        if (!FlightGlobals.ActiveVessel.isEVA && !internalAccess) {
          ScreenMessaging.ShowPriorityScreenMessage(
              "This storage is not accessible from the inside !");
          return;
        }
      }

      // Create icons viewer
      foreach (KeyValuePair<int, KIS_Item> item in items) {
        item.Value.EnableIcon(itemIconResolution);
      }
      EnableIcon();

      // TODO(ihsoft): Don't limit to one open inventory. Add till bootom is reached.
      if (GetAllOpenInventories().Count == 1
          && Mathf.Approximately(guiMainWindowPos.x, defaultFlightPos.x)
          && Mathf.Approximately(guiMainWindowPos.y, defaultFlightPos.y)) {
        guiMainWindowPos.y += 250;
      }
      if (openAnim) {
        openAnim[openAnimName].speed = openAnimSpeed;
        openAnim.Play(openAnimName);
      }
      showGui = true;
      if (HighLogic.LoadedSceneIsEditor) {
        PlaySound(openSndPath);
      } else {
        PlaySound(openSndPath, false, false);
      }
    }
  }

  public bool SetHelmet(bool active, bool checkAtmo = false) {
    if (checkAtmo) {
      if (!this.part.vessel.mainBody.atmosphereContainsOxygen) {
        helmetEquipped = true;
        ScreenMessaging.ShowPriorityScreenMessage(
            "Cannot remove helmet, atmosphere does not contain oxygen !");
        return false;
      }
      if (FlightGlobals.getStaticPressure() < KISAddonConfig.breathableAtmoPressure) {
        helmetEquipped = true;
        ScreenMessaging.ShowPriorityScreenMessage(
            "Cannot remove helmet, pressure is less than {0} ! (Current : {1})",
            KISAddonConfig.breathableAtmoPressure, FlightGlobals.getStaticPressure());
        return false;
      }
    }

    //Disable helmet and visor
    var skmrs =
        new List<SkinnedMeshRenderer>(this.part.GetComponentsInChildren<SkinnedMeshRenderer>());
    foreach (var skmr in skmrs) {
      if (skmr.name == "helmet" || skmr.name == "visor") {
        skmr.GetComponent<Renderer>().enabled = active;
        helmetEquipped = active;
      }
    }

    //Disable flares and light
    var lights = new List<Light>(part.GetComponentsInChildren<Light>(true) as Light[]);
    foreach (var light in lights) {
      if (light.name == "headlamp") {
        light.enabled = active;
        light.transform.Find("flare1").GetComponent<Renderer>().enabled = active;
        light.transform.Find("flare2").GetComponent<Renderer>().enabled = active;
      }
    }

    return true;
  }

  void GUIStyles() {
    GUI.skin = HighLogic.Skin;
    GUI.skin.button.alignment = TextAnchor.MiddleCenter;

    boxStyle = new GUIStyle(GUI.skin.box);
    boxStyle.fontSize = 11;
    boxStyle.padding.top = GUI.skin.box.padding.bottom = GUI.skin.box.padding.left = 5;
    boxStyle.alignment = TextAnchor.UpperLeft;

    upperRightStyle = new GUIStyle(GUI.skin.label);
    upperRightStyle.alignment = TextAnchor.UpperRight;
    upperRightStyle.fontSize = 9;
    upperRightStyle.normal.textColor = Color.yellow;

    upperLeftStyle = new GUIStyle(GUI.skin.label);
    upperLeftStyle.alignment = TextAnchor.UpperLeft;
    upperLeftStyle.fontSize = 11;
    upperLeftStyle.normal.textColor = Color.green;

    lowerRightStyle = new GUIStyle(GUI.skin.label);
    lowerRightStyle.alignment = TextAnchor.LowerRight;
    lowerRightStyle.fontSize = 10;
    lowerRightStyle.padding = new RectOffset(4, 4, 4, 4);
    lowerRightStyle.normal.textColor = Color.white;


    buttonStyle = new GUIStyle(GUI.skin.button);
    buttonStyle.padding = new RectOffset(4, 4, 4, 4);
    buttonStyle.alignment = TextAnchor.MiddleCenter;
  }

  void OnGUI() {
    if (!showGui) {
      return;
    }

    // Update GUI of items
    foreach (KeyValuePair<int, KIS_Item> item in items) {
      item.Value.GUIUpdate();
    }

    GUIStyles();

    // Set title
    string title = this.part.partInfo.title;
    if (invType == InventoryType.Pod) {
      title = part.partInfo.title + " | Seat " + podSeat;
      if (!HighLogic.LoadedSceneIsEditor) {
        ProtoCrewMember crewAtPodSeat = this.part.protoModuleCrew.Find(x => x.seatIdx == podSeat);
        if (crewAtPodSeat != null) {
          title = crewAtPodSeat.name;
        }
      }
    }
    if (invType == InventoryType.Eva) {
      title = this.part.partInfo.title + " | " + kerbalTrait;
    }
    if (invType == InventoryType.Container && invName != "") {
      title = this.part.partInfo.title + " | " + invName;
    }

    guiMainWindowPos = GUILayout.Window(GetInstanceID(), guiMainWindowPos, GuiMain, title);

    if (tooltipItem != null) {
      if (contextItem == null) {
        string tooltipName = tooltipItem.availablePart.title;
        if (tooltipItem.inventoryName != "")
          tooltipName += " | " + tooltipItem.inventoryName;
        GUILayout.Window(GetInstanceID() + 780,
                         new Rect(Event.current.mousePosition.x + 5,
                                  Event.current.mousePosition.y + 5, 400, 1),
                         GuiTooltip, tooltipName);
      }
    }
    if (contextItem != null) {
      var contextRelativeRect = new Rect(
          guiMainWindowPos.x + contextRect.x + (contextRect.width / 2),
          guiMainWindowPos.y + contextRect.y + (contextRect.height / 2),
          80, 10);
      GUILayout.Window(GetInstanceID() + 781, contextRelativeRect, GuiContextMenu, "Action");
      if (contextClick) {
        contextClick = false;
        splitQty = 1;
      } else if (Event.current.type == EventType.MouseDown) {
        contextItem = null;
      }
    }

    if (debugItem != null) {
      guiDebugWindowPos =
          GUILayout.Window(GetInstanceID() + 782, guiDebugWindowPos, GuiDebugItem, "Debug item");
    }

    // Disable Click through
    if (HighLogic.LoadedSceneIsEditor) {
      if (guiMainWindowPos.Contains(Event.current.mousePosition) && !clickThroughLocked) {
        InputLockManager.SetControlLock(
            ControlTypes.EDITOR_PAD_PICK_PLACE, "KISInventoryEditorLock");
        clickThroughLocked = true;
      }
      if (!guiMainWindowPos.Contains(Event.current.mousePosition) && clickThroughLocked) {
        InputLockManager.RemoveControlLock("KISInventoryEditorLock");
        clickThroughLocked = false;
      }
    } else if (HighLogic.LoadedSceneIsFlight) {
      if (guiMainWindowPos.Contains(Event.current.mousePosition) && !clickThroughLocked) {
        InputLockManager.SetControlLock(
            ControlTypes.CAMERACONTROLS | ControlTypes.MAP, "KISInventoryFlightLock");
        clickThroughLocked = true;
      }
      if (!guiMainWindowPos.Contains(Event.current.mousePosition) && clickThroughLocked) {
        InputLockManager.RemoveControlLock("KISInventoryFlightLock");
        clickThroughLocked = false;
      }
    }
  }

  void GuiMain(int windowID) {
    var guiStyleTitle = new GUIStyle(GUI.skin.label);
    guiStyleTitle.normal.textColor = Color.yellow;
    guiStyleTitle.fontStyle = FontStyle.Bold;
    guiStyleTitle.fontSize = 13;
    guiStyleTitle.alignment = TextAnchor.MiddleCenter;

    GUILayout.BeginHorizontal();

    GUILayout.BeginVertical();
    const int Width = 160;
    GUILayout.Box("", GUILayout.Width(Width), GUILayout.Height(100));
    Rect textureRect = GUILayoutUtility.GetLastRect();
    GUI.DrawTexture(textureRect, icon.texture, ScaleMode.ScaleToFit);

    int extraSpace = 0;
    //Set inventory name
    if (invType == InventoryType.Container) {
      if (guiSetName) {
        GUILayout.BeginHorizontal();
        invName = GUILayout.TextField(invName, 14, GUILayout.Height(22));
        if (GUILayout.Button(new GUIContent("OK", ""), GUILayout.Width(30), GUILayout.Height(22))) {
          guiSetName = false;
        }
        GUILayout.EndHorizontal();
      } else {
        if (GUILayout.Button(new GUIContent("Set name", ""),
                             GUILayout.Width(Width), GUILayout.Height(22))) {
          guiSetName = true;
        }
      }
    } else if (invType == InventoryType.Eva) {
      if (helmetEquipped) {
        if (GUILayout.Button(new GUIContent("Remove Helmet", ""),
                             GUILayout.Width(Width), GUILayout.Height(22))) {
          if (SetHelmet(false, true)) {
            PlaySound(helmetOffSndPath);
          }
        }
      } else {
        if (GUILayout.Button(new GUIContent("Put On Helmet", ""),
                             GUILayout.Width(Width), GUILayout.Height(22))) {
          if (SetHelmet(true)) {
            PlaySound(helmetOnSndPath);
          }
        }
      }
    } else {
      extraSpace = 30;
    }

    if (slotsY == 5 && slotSize == 50) {
      extraSpace += 50;
    }

    var sb = new StringBuilder();
    sb.AppendLine("Volume : " + this.totalVolume.ToString("0.00")
                  + "/" + this.maxVolume.ToString("0.00 L"));
    sb.AppendLine("Mass : " + this.part.mass.ToString("0.000"));
    sb.AppendLine("Cost : " + (this.GetContentCost() + part.partInfo.cost) + " √");
    GUILayout.Box(sb.ToString(), boxStyle,
                  GUILayout.Width(Width), GUILayout.Height(45 + extraSpace));
    bool closeInv = false;

    if (GUILayout.Button(new GUIContent("Close", "Close container"),
                         GUILayout.Width(Width), GUILayout.Height(21))) {
      closeInv = true;
    }
    GUILayout.EndVertical();

    GUILayout.BeginVertical();
    GuiInventory();
    GUILayout.EndVertical();

    GUILayout.EndHorizontal();

    if (contextItem == null) {
      GUI.DragWindow(new Rect(0, 0, 10000, 30));
    }
    if (closeInv) {
      ShowInventory();
    }
  }

  void GuiTooltip(int windowID) {
    if (tooltipItem == null) {
      return;
    }

    GUILayout.BeginHorizontal();

    GUILayout.BeginVertical();
    GUILayout.Box("", GUILayout.Width(100), GUILayout.Height(100));
    Rect textureRect = GUILayoutUtility.GetLastRect();
    GUI.DrawTexture(textureRect, tooltipItem.icon.texture, ScaleMode.ScaleToFit);
    GUILayout.EndVertical();

    GUILayout.BeginVertical();
    StringBuilder sb = new StringBuilder();
    sb.AppendLine("Volume : " + tooltipItem.volume.ToString("0.00 L"));
    sb.AppendLine("Dry mass : " + tooltipItem.dryMass.ToString("0.000"));
    if (tooltipItem.availablePart.partPrefab.Resources.Count > 0) {
      sb.AppendLine("Ressource mass : " + tooltipItem.resourceMass.ToString("0.000"));
    }
    sb.AppendLine("Cost : " + tooltipItem.cost + " √");
    if (tooltipItem.contentCost > 0) {
      sb.AppendLine("Content cost : " + tooltipItem.contentCost + " √");
    }
    if (tooltipItem.contentMass > 0) {
      sb.AppendLine("Content mass : " + tooltipItem.contentMass.ToString("0.000"));
    }
    if (tooltipItem.equipSlot != null) {
      sb.AppendLine("Equip slot : " + tooltipItem.equipSlot);
      if (tooltipItem.equipSlot == "rightHand")
        sb.AppendLine("Press [" + evaRightHandKey + "] to use (equipped)");
    }
    GUILayout.Box(sb.ToString(), boxStyle, GUILayout.Width(150), GUILayout.Height(100));
    GUILayout.EndVertical();

    GUILayout.BeginVertical();
    StringBuilder text2 = new StringBuilder();

    if (tooltipItem.quantity > 1) {
      // Show total if stacked
      GUI.Label(textureRect, "x" + tooltipItem.quantity.ToString() + " ", lowerRightStyle);
      text2.AppendLine("Total cost : " + tooltipItem.totalCost + " √");
      text2.AppendLine("Total volume : " + tooltipItem.stackVolume.ToString("0.00 L"));
      text2.AppendLine("Total mass : " + tooltipItem.totalMass);
    } else {
      // Show resource if not stacked
      List<KIS_Item.ResourceInfo> resources = tooltipItem.GetResources();
      if (resources.Count > 0) {
        foreach (KIS_Item.ResourceInfo resource in resources) {
          text2.AppendLine(resource.resourceName + " : " + resource.amount.ToString("0.000") + " / "
                           + resource.maxAmount.ToString("0.000"));
        }
      } else {
        text2.AppendLine("Part has no resources");
      }
    }

    // Show science data
    List<ScienceData> sciences = tooltipItem.GetSciences();
    if (sciences.Count > 0) {
      foreach (ScienceData scienceData in sciences) {
        text2.AppendLine(scienceData.title + " (Data=" + scienceData.dataAmount.ToString("0.00")
                         //+ ",Value=" + scienceData.TransmitValue.ToString("0.00") + ")");
                         + ",Value=" + (scienceData.baseTransmitValue * scienceData.transmitBonus).ToString("0.00") + ")");
      }
    } else {
      text2.AppendLine("Part has no science data");
    }


    GUILayout.Box(text2.ToString(), boxStyle, GUILayout.Width(200), GUILayout.Height(100));
    GUILayout.EndVertical();

    GUILayout.EndHorizontal();
    GUILayout.Space(10);
    GUILayout.Box(tooltipItem.availablePart.description, boxStyle,
                  GUILayout.Width(450), GUILayout.Height(100));
  }

  void GuiContextMenu(int windowID) {
    GUI.FocusWindow(windowID);
    GUI.BringWindowToFront(windowID);
    bool noAction = true;

    //Equip
    if (contextItem != null) {
      if (contextItem.equipable && invType == InventoryType.Eva) {
        noAction = false;
        if (contextItem.equipped) {
          if (GUILayout.Button("Unequip")) {
            contextItem.Unequip();
            contextItem = null;
          }
        } else {
          if (GUILayout.Button("Equip")) {
            contextItem.Equip();
            contextItem = null;
          }
        }
      }
    }

    //Carriable
    if (contextItem != null) {
      if (contextItem.carriable && invType == InventoryType.Eva) {
        noAction = false;
        if (GUILayout.Button("Drop")) {
          contextItem.Drop();
          contextItem = null;
        }
      }
    }

    //Set stack quantity (editor only)
    if (contextItem != null && HighLogic.LoadedSceneIsEditor) {
      if (contextItem.stackable) {
        noAction = false;
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("-", buttonStyle, GUILayout.Width(20))) {
          if (Input.GetKey(KeyCode.LeftShift)) {
            if (contextItem.quantity - 10 > 0) {
              if (contextItem.StackRemove(10) == false)
                contextItem = null;
            }
          } else if (Input.GetKey(KeyCode.LeftControl)) {
            if (contextItem.quantity - 100 > 0) {
              if (contextItem.StackRemove(100) == false)
                contextItem = null;
            }
          } else {
            if (contextItem.quantity - 1 > 0) {
              if (contextItem.StackRemove(1) == false)
                contextItem = null;
            }
          }
        }
        if (GUILayout.Button("+", buttonStyle, GUILayout.Width(20))) {
          if (contextItem.stackable) {
            if (Input.GetKey(KeyCode.LeftShift)) {
              contextItem.StackAdd(10);
            } else if (Input.GetKey(KeyCode.LeftControl)) {
              contextItem.StackAdd(100);
            } else {
              contextItem.StackAdd(1);
            }
          }
        }
        if (contextItem != null)
          GUILayout.Label("Quantity : " + contextItem.quantity, GUILayout.Width(100));
        GUILayout.EndHorizontal();
      }
    }

    //Split
    if (contextItem != null && !HighLogic.LoadedSceneIsEditor) {
      if (contextItem.quantity > 1) {
        noAction = false;
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("-", buttonStyle, GUILayout.Width(20))) {
          if (splitQty - 1 > 0)
            splitQty -= 1;
        }
        if (GUILayout.Button("Split (" + splitQty + ")", buttonStyle)) {
          if (this.isFull() == false) {
            contextItem.quantity -= splitQty;
            AddItem(contextItem.availablePart.partPrefab, splitQty);
          } else {
            ScreenMessaging.ShowPriorityScreenMessage("Inventory is full, cannot split !");
          }
          contextItem = null;
        }
        if (GUILayout.Button("+", buttonStyle, GUILayout.Width(20))) {
          if (splitQty + 1 < contextItem.quantity)
            splitQty += 1;
        }
        GUILayout.EndHorizontal();
      }
    }

    // Use
    if (contextItem != null) {
      if ((HighLogic.LoadedSceneIsFlight && contextItem.usableFromEva
           && contextItem.inventory.invType == InventoryType.Eva)
          || (HighLogic.LoadedSceneIsFlight && contextItem.usableFromContainer
              && contextItem.inventory.invType == InventoryType.Container)
          || (HighLogic.LoadedSceneIsFlight && contextItem.usableFromPod
              && contextItem.inventory.invType == InventoryType.Pod)
          || (HighLogic.LoadedSceneIsEditor && contextItem.usableFromEditor)) {
        noAction = false;
        if (GUILayout.Button(contextItem.prefabModule.useName)) {
          contextItem.Use(KIS_Item.UseFrom.ContextMenu);
          contextItem = null;
        }
      }
    }

    //Debug
    if (debugContextMenu) {
      if (contextItem != null && !HighLogic.LoadedSceneIsEditor && invType == InventoryType.Eva) {
        if (contextItem.prefabModule != null) {
          noAction = false;
          if (GUILayout.Button("Debug")) {
            debugItem = contextItem;
            contextItem = null;
          }
        }
      }
    }
    if (noAction) {
      GUILayout.Label("No action");
    }
  }

  void GuiDebugItem(int windowID) {
    if (debugItem != null) {
      KIS_Shared.EditField("moveSndPath", ref debugItem.prefabModule.moveSndPath);
      KIS_Shared.EditField("shortcutKeyAction(drop,equip,custom)",
                           ref debugItem.prefabModule.shortcutKeyAction);
      KIS_Shared.EditField("usableFromEva", ref debugItem.prefabModule.usableFromEva);
      KIS_Shared.EditField("usableFromContainer", ref debugItem.prefabModule.usableFromContainer);
      KIS_Shared.EditField("usableFromPod", ref debugItem.prefabModule.usableFromPod);
      KIS_Shared.EditField("usableFromEditor", ref debugItem.prefabModule.usableFromEditor);
      KIS_Shared.EditField("useName", ref debugItem.prefabModule.useName);
      KIS_Shared.EditField("equipMode(model,physic)", ref debugItem.prefabModule.equipMode);
      KIS_Shared.EditField("equipSlot", ref debugItem.prefabModule.equipSlot);
      KIS_Shared.EditField("equipable", ref debugItem.prefabModule.equipable);
      KIS_Shared.EditField("stackable", ref debugItem.prefabModule.stackable);
      KIS_Shared.EditField("carriable", ref debugItem.prefabModule.carriable);
      KIS_Shared.EditField("equipSkill(<blank>,RepairSkill,ScienceSkill,etc...)",
                           ref debugItem.prefabModule.equipSkill);
      KIS_Shared.EditField("equipRemoveHelmet", ref debugItem.prefabModule.equipRemoveHelmet);
      KIS_Shared.EditField("volumeOverride(0 = auto)", ref debugItem.prefabModule.volumeOverride);

      scrollPositionDbg = GUILayout.BeginScrollView(scrollPositionDbg,
                                                    GUILayout.Width(400), GUILayout.Height(200));
      var skmrs =
          new List<SkinnedMeshRenderer>(this.part.GetComponentsInChildren<SkinnedMeshRenderer>());
      foreach (var skmr in skmrs) {
        GUILayout.Label("--- " + skmr.name + " ---");
        foreach (var bone in skmr.bones) {
          if (GUILayout.Button(new GUIContent(bone.name, ""))) {
            debugItem.prefabModule.equipMeshName = skmr.name;
            debugItem.prefabModule.equipBoneName = bone.name;
            debugItem.ReEquip();
          }
        }
      }

      GUILayout.EndScrollView();
      if (KIS_Shared.EditField("equipPos", ref debugItem.prefabModule.equipPos)) {
        debugItem.ReEquip();
      }
      if (KIS_Shared.EditField("equipDir", ref debugItem.prefabModule.equipDir)) {
        debugItem.ReEquip();
      }
    }
    if (GUILayout.Button("Close")) {
      debugItem = null;
    }
    GUI.DragWindow();
  }

 void GuiInventory() {
    int slotIndex = 0;
    KIS_Item mouseOverItem = null;
    for (var x = 0; x < slotsY; x++) {
      GUILayout.BeginHorizontal();
      for (var y = 0; y < slotsX; y++) {
        GUILayout.Box("", GUILayout.Width(slotSize), GUILayout.Height(slotSize));
        Rect textureRect = GUILayoutUtility.GetLastRect();
        if (items.ContainsKey(slotIndex)) {
          GuiHandleUsedSlot(textureRect, slotIndex);
          GuiUpdateHoveredItem(textureRect, slotIndex, ref mouseOverItem);
        } else {
          GuiHandleEmptySlot(textureRect, slotIndex);
        }
        slotIndex++;
      }
      GUILayout.EndHorizontal();
    }
    tooltipItem = mouseOverItem;
  }

  /// <summary>Handles mouse over a slot.</summary>
  /// <param name="textureRect">Slot's rect in UI.</param>
  /// <param name="slotIndex">Slot's index in inventory.</param>
  /// <param name="mouseOverItem">[out] Hovered item if any.</param>
  void GuiUpdateHoveredItem(Rect textureRect, int slotIndex, ref KIS_Item mouseOverItem) {
    // Mouse over a slot
    if (Event.current.type == EventType.Repaint
        && textureRect.Contains(Event.current.mousePosition)
        && !KISAddonPickup.draggedPart) {
      mouseOverItem = items[slotIndex];
      mouseOverItem.icon.Rotate();
      if (mouseOverItem != tooltipItem && tooltipItem != null) {
        tooltipItem.icon.ResetPos();
      }
    }
  }

  /// <summary>Handles GUI drawing and events in a slot with item.</summary>
  /// <param name="textureRect">Slot's rect in UI.</param>
  /// <param name="slotIndex">Slot's index in inventory.</param>
  void GuiHandleUsedSlot(Rect textureRect, int slotIndex) {
    GUI.DrawTexture(textureRect, items[slotIndex].icon.texture, ScaleMode.ScaleToFit);
    if (HighLogic.LoadedSceneIsFlight) {
      if (FlightGlobals.ActiveVessel.isEVA
          && FlightGlobals.ActiveVessel == this.part.vessel) {
        // Keyboard shortcut
        int slotNb = slotIndex + 1;
        GUI.Label(textureRect, " " + slotNb.ToString(), upperLeftStyle);
        if (items[slotIndex].carried) {
          GUI.Label(textureRect, " Carried  ", upperRightStyle);
        } else if (items[slotIndex].equipped) {
          GUI.Label(textureRect, " Equip.  ", upperRightStyle);
        }
      }
    }
    if (items[slotIndex].stackable) {
      // Quantity
      GUI.Label(textureRect, "x" + items[slotIndex].quantity.ToString() + "  ", lowerRightStyle);
    }

    if (Event.current.type == EventType.MouseDown
        && textureRect.Contains(Event.current.mousePosition)) {
      // Pickup part
      if (Event.current.button == 0) {
        KISAddonPickup.instance.Pickup(items[slotIndex]);
      }
      // Context menu
      if (Event.current.button == 1) {
        contextClick = true;
        contextItem = items[slotIndex];
        contextRect = textureRect;
        contextSlot = slotIndex;
      }
    }

    // Mouse up on used slot
    if (Event.current.type == EventType.MouseUp && Event.current.button == 0
        && textureRect.Contains(Event.current.mousePosition) && KISAddonPickup.draggedPart) {
      if (KISAddonPickup.draggedItem != items[slotIndex]) {
        ModuleKISInventory srcInventory = null;
        if (items[slotIndex].stackable
            && items[slotIndex].availablePart.name == KISAddonPickup.draggedPart.partInfo.name) {
          // Stack similar item
          if (KISAddonPickup.draggedItem != null) {
            srcInventory = KISAddonPickup.draggedItem.inventory;
            // Part come from inventory
            bool checkVolume = true;
            if (srcInventory == this) {
              checkVolume = false;
            }
            if (items[slotIndex].StackAdd(KISAddonPickup.draggedItem.quantity, checkVolume)) {
              KISAddonPickup.draggedItem.Delete();
              items[slotIndex].OnMove(srcInventory, this);
            }
          } else {
            // Part come from scene
            if (items[slotIndex].StackAdd(1)) {
              KISAddonPickup.draggedPart.Die();
              items[slotIndex].OnMove(srcInventory, this);
            }
          }
        } else {
          // Exchange part slot
          if (KISAddonPickup.draggedItem != null) {
            if (KISAddonPickup.draggedItem.inventory == items[slotIndex].inventory) {
              KIS_Item srcItem = KISAddonPickup.draggedItem;
              int srcSlot = srcItem.slot;
              srcInventory = KISAddonPickup.draggedItem.inventory;

              KIS_Item destItem = items[slotIndex];
              int destSlot = slotIndex;
              ModuleKISInventory destInventory = this;

              // Move src to dest
              destInventory.items.Remove(destSlot);
              destInventory.items.Add(destSlot, srcItem);
              srcItem.inventory = destInventory;
              destInventory.RefreshMassAndVolume();

              // Move dest to src
              srcInventory.items.Remove(srcSlot);
              srcInventory.items.Add(srcSlot, destItem);
              destItem.inventory = srcInventory;
              srcInventory.RefreshMassAndVolume();
              items[slotIndex].OnMove(srcInventory, this);
            }
          }
        }
      }
    }
  }

  /// <summary>Handles GUI drawing and events in an empty slot.</summary>
  /// <param name="textureRect">Slot's rect in UI.</param>
  /// <param name="slotIndex">Slot's index in inventory.</param>
  void GuiHandleEmptySlot(Rect textureRect, int slotIndex) {
    // Mouse up on a free slot
    if (Event.current.type == EventType.MouseUp && Event.current.button == 0
        && textureRect.Contains(Event.current.mousePosition) && KISAddonPickup.draggedPart) {
      // Check if part can be carried
      bool carryPart = false;
      bool storePart = true;
      var draggedItemModule = KISAddonPickup.draggedPart.GetComponent<ModuleKISItem>();
      if (!draggedItemModule && KISAddonPickup.draggedItem != null) {
        draggedItemModule = KISAddonPickup.draggedItem.prefabModule;
      }

      if (draggedItemModule && draggedItemModule.carriable) {
        if (HighLogic.LoadedSceneIsEditor && podSeat != -1) {
          ScreenMessaging.ShowPriorityScreenMessage(
              "Carriable items cannot be stored in the seat's inventory");
          storePart = false;
        } else if (HighLogic.LoadedSceneIsFlight && invType == InventoryType.Eva) {
          carryPart = true;
          foreach (KeyValuePair<int, KIS_Item> enumeratedItem in items) {
            if (enumeratedItem.Value.equipSlot == draggedItemModule.equipSlot
                && enumeratedItem.Value.carriable) {
              if (KISAddonPickup.draggedItem != null) {
                // Ignore self
                if (enumeratedItem.Value == KISAddonPickup.draggedItem) {
                  break;
                }
              }
              carryPart = false;
              storePart = false;
              ScreenMessaging.ShowPriorityScreenMessage(
                  "Another part is already carried on slot <{0}>", draggedItemModule.equipSlot);
              break;
            }
          }
        }
      }

      // Store item or part
      if (storePart) {
        if (KISAddonPickup.draggedItem != null) {
          // Picked part from an inventory
          if (carryPart) {
            MoveItem(KISAddonPickup.draggedItem, this, slotIndex);
            if (!KISAddonPickup.draggedItem.equipped) {
              KISAddonPickup.draggedItem.Equip();
            }
          } else {
            if (VolumeAvailableFor(KISAddonPickup.draggedItem)) {
              MoveItem(KISAddonPickup.draggedItem, this, slotIndex);
            }
          }
        } else if (KISAddonPickup.draggedPart != part) {
          // Picked part from scene
          if (carryPart) {
            KIS_Shared.SendKISMessage(
                KISAddonPickup.draggedPart, KIS_Shared.MessageAction.Store);
            KIS_Item carryItem = AddItem(KISAddonPickup.draggedPart, 1, slotIndex);
            KISAddonPickup.draggedPart.Die();
            carryItem.Equip();
          } else {
            if (VerifyIsNotAssembly(KISAddonPickup.draggedPart)
                && VolumeAvailableFor(KISAddonPickup.draggedPart)) {
              KIS_Shared.SendKISMessage(
                  KISAddonPickup.draggedPart, KIS_Shared.MessageAction.Store);
              AddItem(KISAddonPickup.draggedPart, 1, slotIndex);
              if (!HighLogic.LoadedSceneIsEditor) {
                KISAddonPickup.draggedPart.Die();
              }
            }
          }
        }
      }
    }
  }

  // Sets icon, ensuring any old icon is Disposed
  private void EnableIcon() {
    DisableIcon();
    icon = new KIS_IconViewer(part, selfIconResolution);
  }

  // Clears icon, ensuring it is Disposed
  private void DisableIcon() {
    if (icon != null) {
      icon.Dispose();
      icon = null;
    }
  }


  /// <summary>Hides all UI elements.</summary>
  void OnTooltipDestroyRequestedEvent() {
    showGui = false;
  }
}
  
}  // namespace
