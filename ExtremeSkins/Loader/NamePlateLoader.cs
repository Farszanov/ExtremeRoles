using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Text;

using BepInEx.Configuration;

using UnityEngine;

using Newtonsoft.Json.Linq;

using ExtremeSkins.Core;
using ExtremeSkins.Core.ExtremeNamePlate;
using ExtremeSkins.Module;

namespace ExtremeSkins.Loader;

public sealed class NamePlateLoader : ICosmicLoader
{
	private const string repo = "https://raw.githubusercontent.com/yukieiji/ExtremeNamePlate/main"; // When using this repository with Fork, please follow the license of each hat
	private const string skinDlUrl = "https://github.com/yukieiji/ExtremeNamePlate/archive/refs/heads/main.zip";

	private const string workingFolder = "ExNWorking";
	private const string dlZipName = "ExtremeNamePlate-main.zip";
	private const string namePlateDataPath = @"ExtremeNamePlate-main\namePlate";

	private const string namePlateRepoData = "namePlateData.json";
	private const string namePlateTransData = "namePlateTransData.json";

	private const string updateComitKey = "ExNUpdateComitHash";
	private const string jsonUpdateComitKey = "updateComitHash";
	private const string defaultHash = "NoHashData";

	private readonly ConfigEntry<string> curUpdateHash;

	public NamePlateLoader()
	{
		this.curUpdateHash = ExtremeSkinsPlugin.Instance.Config.Bind(
			ExtremeSkinsPlugin.SkinComitCategory,
			updateComitKey, defaultHash);
	}

	public IEnumerator Fetch()
	{
		if (!this.isUpdate())
		{
			yield break;
		}

		var logger = ExtremeSkinsPlugin.Logger;
		logger.LogInfo("---------- Extreme NamePlate Manager : NamePlateData Download Start!! ---------- ");

		string? ausFolder = Path.GetDirectoryName(Application.dataPath);
		if (string.IsNullOrEmpty(ausFolder))
		{
			yield break;
		}

		string dataSaveFolder = Path.Combine(ausFolder, DataStructure.FolderName);

		cleanUpCurSkinData(dataSaveFolder);

		string dlFolder = Path.Combine(ausFolder, workingFolder);

		Helper.FileUtility.DeleteDir(dlFolder);
		Directory.CreateDirectory(dlFolder);

		string zipPath = Path.Combine(dlFolder, dlZipName);

		yield return Helper.FileUtility.DlToZip(skinDlUrl, zipPath);
		if (!File.Exists(zipPath))
		{
			logger.LogInfo(
				"---------- Extreme NamePlate Manager : NamePlateData Download ERROR ---------- ");
			yield break;
		}

		logger.LogInfo("---------- Extreme NamePlate Manager : NamePlateData Download Complete!! ---------- ");

		logger.LogInfo("---------- Extreme NamePlate Manager : NamePlateData Install Start!! ---------- ");

		installNamePlateData(dlFolder, zipPath, dataSaveFolder);

		logger.LogInfo("---------- Extreme NamePlate Manager : NamePlateData Install Complete!! ---------- ");
#if RELEASE
        Helper.FileUtility.DeleteDir(dlFolder);
#endif
	}

	public IReadOnlyDictionary<string, T> Load<T>() where T : class
	{
		if (typeof(T) != typeof(CustomNamePlate))
		{
			throw new System.ArgumentException(
				$"Type {typeof(T)} is not supported.");
		}
		var result = new Dictionary<string, T>();

		ExtremeSkinsPlugin.Logger.LogInfo("---------- Extreme NamePlate Manager : NamePlate Loading Start!! ----------");

		getJsonData(namePlateTransData).GetAwaiter().GetResult();

		string? folderPath = Path.GetDirectoryName(Application.dataPath);
		if (string.IsNullOrEmpty(folderPath)) { return result; }

		string installFolder = Path.Combine(folderPath, DataStructure.FolderName);

		Helper.Translation.UpdateHatsTransData(
			Path.Combine(installFolder, namePlateTransData));

		// UpdateComitHash
		byte[] byteNpArray = File.ReadAllBytes(
			Path.Combine(installFolder, namePlateRepoData));

		curUpdateHash.Value = (string)(JObject.Parse(
			Encoding.UTF8.GetString(byteNpArray))[jsonUpdateComitKey]);

		string[] namePlateFolder = Directory.GetDirectories(installFolder);

		foreach (string authorPath in namePlateFolder)
		{
			if (string.IsNullOrEmpty(authorPath)) { continue; }

			string[] authorDirs = authorPath.Split(@"\");
			string author = authorDirs[^1];

			string[] namePlateImage = Directory.GetFiles(
				authorPath, "*.png");

			foreach (string namePlate in namePlateImage)
			{
				string[] namePlateDir = namePlate.Split(@"\");
				string imageName = namePlateDir[^1];

				CustomNamePlate customNamePlate = new CustomNamePlate(
					namePlate, author,
					imageName.Substring(0, imageName.Length - 4));
				var castedNp = Unsafe.As<CustomNamePlate, T>(ref customNamePlate);
				if (result.TryAdd(customNamePlate.Id, castedNp))
				{
					ExtremeSkinsPlugin.Logger.LogInfo(
						$"NamePlate Loaded:\n{customNamePlate}");
				}
			}

		}

		ExtremeSkinsPlugin.Logger.LogInfo("---------- Extreme NamePlate Manager : NamePlate Loading Complete!! ----------");

		return result;
	}

	private static async Task getJsonData(string fileName)
	{
		await ICosmicLoader.getData(
			$"{repo}/namePlate/{fileName}",
			Path.Combine(DataStructure.FolderName, fileName));
	}

	private static void cleanUpCurSkinData(
		string dataSaveFolder)
	{

		Helper.FileUtility.DeleteDir(dataSaveFolder);
		Directory.CreateDirectory(dataSaveFolder);

		getJsonData(namePlateRepoData).GetAwaiter().GetResult();

		byte[] byteNpArray = File.ReadAllBytes(
			Path.Combine(dataSaveFolder, namePlateRepoData));
		string npJsonString = Encoding.UTF8.GetString(byteNpArray);

		JObject npFolder = JObject.Parse(npJsonString);

		for (int i = 0; i < npFolder.Count; ++i)
		{
			JProperty? token = npFolder.ChildrenTokens[i].TryCast<JProperty>();
			if (token == null) { continue; }

			string author = token.Name;

			if (author == jsonUpdateComitKey ||
				author == namePlateRepoData ||
				author == namePlateTransData) { continue; }

			string checkNpFolder = string.Concat(dataSaveFolder, author);

			// まずはフォルダとファイルを消す
			if (Directory.Exists(checkNpFolder))
			{
				string[] filePaths = Directory.GetFiles(checkNpFolder);
				foreach (string filePath in filePaths)
				{
					File.SetAttributes(filePath, FileAttributes.Normal);
					File.Delete(filePath);
				}
				Directory.Delete(checkNpFolder, false); ;
			}
		}
	}

	private static void installNamePlateData(
		string workingDir,
		string zipPath,
		string installFolder)
	{
		string extractPath = Path.Combine(workingDir, "namePlate");
		ZipFile.ExtractToDirectory(zipPath, extractPath);

		string? ausFolder = Path.GetDirectoryName(Application.dataPath);
		if (string.IsNullOrEmpty(ausFolder))
		{
			return;
		}

		byte[] byteNamePlateArray = File.ReadAllBytes(
			Path.Combine(ausFolder, DataStructure.FolderName, namePlateRepoData));
		string namePlateJsonString = Encoding.UTF8.GetString(byteNamePlateArray);

		JObject namePlateFolder = JObject.Parse(namePlateJsonString);

		for (int i = 0; i < namePlateFolder.Count; ++i)
		{
			JProperty? token = namePlateFolder.ChildrenTokens[i].TryCast<JProperty>();
			if (token == null) { continue; }

			string author = token.Name;

			if (author == jsonUpdateComitKey) { continue; }

			string namePlateMoveToFolder = Path.Combine(installFolder, author);
			string namePlateSourceFolder = Path.Combine(extractPath, namePlateDataPath, author);

			ExtremeSkinsPlugin.Logger.LogInfo($"Installing NamePlate:{author} namePlate");

			Directory.Move(namePlateSourceFolder, namePlateMoveToFolder);
		}
	}

	private bool isUpdate()
	{

		ExtremeSkinsPlugin.Logger.LogInfo("Extreme NamePlate Manager : Checking Update....");

		string? folderPath = Path.GetDirectoryName(Application.dataPath);
		if (string.IsNullOrEmpty(folderPath)) { return true; }

		string installFolder = Path.Combine(folderPath, DataStructure.FolderName);
		if (!Directory.Exists(installFolder)) { return true; }

		getJsonData(namePlateRepoData).GetAwaiter().GetResult();

		byte[] byteNamePlateArray = File.ReadAllBytes(
			Path.Combine(installFolder, namePlateRepoData));

		string namePlateJsonString = Encoding.UTF8.GetString(byteNamePlateArray);
		JObject namePlateFolder = JObject.Parse(namePlateJsonString);

		for (int i = 0; i < namePlateFolder.Count; ++i)
		{
			JProperty? token = namePlateFolder.ChildrenTokens[i].TryCast<JProperty>();
			if (token == null) { continue; }

			string author = token.Name;

			if (author == jsonUpdateComitKey)
			{
				string tokenStr = (string)token.Value;
				string curTokenStr = this.curUpdateHash.Value;

				if (string.IsNullOrEmpty(tokenStr) ||
					(curTokenStr != defaultHash && tokenStr != curTokenStr))
				{
					return true;
				}
				else
				{
					continue;
				}
			}

			if (author == namePlateRepoData ||
				author == namePlateTransData)
			{
				continue;
			}

			string checkNamePlateFolder = Path.Combine(installFolder, author);

			if (!Directory.Exists(checkNamePlateFolder) ||
				!File.Exists(Path.Combine(checkNamePlateFolder, License.FileName))) { return true; }

			JArray? namePlateImage = token.Value.TryCast<JArray>();

			if (namePlateImage == null) { return true; }

			for (int j = 0; j < namePlateImage.Count; ++j)
			{

				JValue? value = namePlateImage[j].TryCast<JValue>();
				if (value == null) { continue; }

				string namePlateName = value.Value.ToString();

				if (!File.Exists(Path.Combine(checkNamePlateFolder, $"{namePlateName}.png")) &&
					!Directory.Exists(Path.Combine(checkNamePlateFolder, namePlateName)))
				{
					return true;
				}
			}

		}

		return false;
	}
}
