using System.Collections.Generic;
using System.Linq;

using UnityEngine;
using HarmonyLib;

using ExtremeRoles.Extension.UnityEvents;
using ExtremeRoles.Performance;

using ExtremeSkins.Module;
using ExtremeSkins.Helper;

using AmongUs.Data;
using AmongUs.Data.Player;

using ExRLoader = ExtremeRoles.Resources.UnityObjectLoader;
using System.Reflection;


#nullable enable

namespace ExtremeSkins.Patches.AmongUs.Tab;

#if WITHHAT
[HarmonyPatch]
public static class HatsTabPatch
{
    private static List<TMPro.TMP_Text> hatsTabCustomText = new List<TMPro.TMP_Text>();

    private static float inventoryTop = 1.5f;
    private static float inventoryBottom = -2.5f;

    public static CreatorTab? Tab = null;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(HatsTab), nameof(HatsTab.OnEnable))]
    public static bool HatsTabOnEnablePrefix(HatsTab __instance)
    {
        inventoryTop = __instance.scroller.Inner.position.y - 0.5f;
        inventoryBottom = __instance.scroller.Inner.position.y - 4.5f;

        HatData[] unlockedHats = HatManager.Instance.GetUnlockedHats();
        Dictionary<string, List<HatData>> hatPackage = new Dictionary<string, List<HatData>>();

        CustomCosmicTab.DestoryList(hatsTabCustomText);
        CustomCosmicTab.DestoryList(__instance.ColorChips.ToArray().ToList());
        CustomCosmicTab.RemoveAllTabs();

        hatsTabCustomText.Clear();
        __instance.ColorChips.Clear();

        if (CustomCosmicTab.TextTemplate == null)
        {
            CustomCosmicTab.TextTemplate = Object.Instantiate(
                PlayerCustomizationMenu.Instance.itemName);
            CustomCosmicTab.TextTemplate.gameObject.SetActive(false);
        }

        if (Tab == null)
        {
            GameObject obj = Object.Instantiate(
                ExRLoader.LoadFromResources<GameObject>(
                    CustomCosmicTab.CreatorTabAssetBundle,
                    CustomCosmicTab.CreatorTabAssetPrefab,
					Assembly.GetExecutingAssembly()),
                __instance.transform);
            Tab = obj.GetComponent<CreatorTab>();
            obj.hideFlags |= HideFlags.HideAndDontSave | HideFlags.DontSaveInEditor;
        }

		CustomCosmicTab.EnableTab(__instance);

        foreach (HatData hatBehaviour in unlockedHats)
        {
            if (CosmicStorage<CustomHat>.TryGet(
					hatBehaviour.ProductId, out CustomHat? hat) &&
					hat != null)
            {
                if (!hatPackage.ContainsKey(hat.Author))
                {
                    hatPackage.Add(hat.Author, new List<HatData>());
                }
                hatPackage[hat.Author].Add(hatBehaviour);
            }
            else
            {
                if (!hatPackage.ContainsKey(CustomCosmicTab.InnerslothPackageName))
                {
                    hatPackage.Add(
                        CustomCosmicTab.InnerslothPackageName,
                        new List<HatData>());
                }
                hatPackage[CustomCosmicTab.InnerslothPackageName].Add(hatBehaviour);
            }
        }

        float yOffset = __instance.YStart;

        var orderedKeys = hatPackage.Keys.OrderBy((string x) =>
        {
            if (x == CustomCosmicTab.InnerslothPackageName)
            {
                return 0;
            }
            else
            {
                return 100;
            }
        });

        foreach (string key in orderedKeys)
        {
            createHatTab(hatPackage[key], key, yOffset, __instance);
            yOffset = (yOffset - (CustomCosmicTab.HeaderSize * __instance.YOffset)) - (
                (hatPackage[key].Count - 1) / __instance.NumPerRow) * __instance.YOffset - CustomCosmicTab.HeaderSize;
        }

		__instance.currentHatIsEquipped = true;
		__instance.scroller.ContentYBounds.max = -(yOffset + 3.0f + CustomCosmicTab.HeaderSize);

		Tab.gameObject.SetActive(true);
        Tab.SetUpButtons(__instance.scroller, hatsTabCustomText);
        return false;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(HatsTab), nameof(HatsTab.Update))]
    public static void HatsTabUpdatePostfix()
    {
        CustomCosmicTab.HideTmpTextPackage(
            hatsTabCustomText, inventoryTop, inventoryBottom);
    }

    private static void createHatTab(
        List<HatData> hats, string packageName, float yStart, HatsTab __instance)
    {
        float offset = yStart;

        CustomCosmicTab.AddTmpTextPackageName(
            __instance, yStart, packageName,
            ref hatsTabCustomText, ref offset);

        int numHats = hats.Count;

        PlayerCustomizationData playerSkinData = DataManager.Player.Customization;
		int index = 0;
        foreach (var hat in hats)
        {

            ColorChip colorChip = CustomCosmicTab.SetColorChip(__instance, index, offset);

			var button = colorChip.Button;

			if (ActiveInputManager.currentControlType == ActiveInputManager.InputType.Keyboard)
            {
				button.OnMouseOver.AddListener(() => __instance.SelectHat(hat));
				button.OnMouseOut.AddListener(
                    () =>
                    {
                        __instance.SelectHat(
                            HatManager.Instance.GetHatById(
                                playerSkinData.Hat));
                    });
				button.OnClick.AddListener(() => __instance.ClickEquip());
            }
            else
            {
				button.OnClick.AddListener(() => __instance.SelectHat(hat));
            }

			button.ClickMask = __instance.scroller.Hitbox;
			colorChip.Inner.SetMaskType(PlayerMaterial.MaskType.SimpleUI);
			colorChip.Tag = hat;

			int color = __instance.GetDisplayColor();

			__instance.UpdateMaterials(colorChip.Inner.FrontLayer, hat);
			if (CosmicStorage<CustomHat>.TryGet(
					hat.ProductId, out var customHat) &&
				customHat != null)
			{
				CustomCosmicTab.SetPreviewToRawSprite(colorChip, customHat.Preview, color);
			}
			else
			{
				hat.SetPreview(colorChip.Inner.FrontLayer, color);
			}

			colorChip.SelectionHighlight.gameObject.SetActive(false);
			__instance.ColorChips.Add(colorChip);

			index++;
			if (!HatManager.Instance.CheckLongModeValidCosmetic(
				hat.ProdId, __instance.PlayerPreview.GetIgnoreLongMode()))
			{
				colorChip.SetUnavailable();
			}
		}
    }
}
#endif
