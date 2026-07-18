using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Xps.Serialization;
using System.Xml;
using System.Xml.Serialization;

namespace MojiCollaTool
{
    /// <summary>
    /// 旧単ページ形式を検証済みの一時領域に読み込んだ結果。
    /// CommitProjectDataToWorkingDir が成功するまで既存のWorking領域は変更しない。
    /// </summary>
    public sealed class LegacyProjectData : IDisposable
    {
        private string? _stagingDirectoryPath;

        internal LegacyProjectData(string stagingDirectoryPath, CanvasData canvasData, IReadOnlyList<MojiData> mojiDatas)
        {
            _stagingDirectoryPath = stagingDirectoryPath;
            CanvasData = canvasData;
            MojiDatas = mojiDatas;
        }

        public CanvasData CanvasData { get; }

        public IReadOnlyList<MojiData> MojiDatas { get; }

        internal string StagingDirectoryPath => _stagingDirectoryPath
            ?? throw new InvalidOperationException("The legacy project data has already been committed.");

        internal void MarkCommitted()
        {
            _stagingDirectoryPath = null;
        }

        public void Dispose()
        {
            if (_stagingDirectoryPath == null) return;

            try
            {
                if (Directory.Exists(_stagingDirectoryPath))
                {
                    Directory.Delete(_stagingDirectoryPath, recursive: true);
                }
            }
            catch
            {
                // 一時領域の後始末失敗は、本来の保存・読込結果を二次障害にしない。
            }
            finally
            {
                _stagingDirectoryPath = null;
            }
        }
    }

    public class DataIO
    {
        private const int MaxLegacyArchiveEntries = 1024;
        private const long MaxLegacyEntrySize = 64L * 1024 * 1024;
        private const long MaxLegacyArchiveSize = 256L * 1024 * 1024;

        /// <summary>
        /// エラーログを書き出す
        /// </summary>
        /// <param name="message"></param>
        public static void WriteErrorLog(string message)
        {
            try
            {
                var errorLogDirPath = Path.Combine(GetExeDirPath(), "ErrorLog");

                if (Directory.Exists(errorLogDirPath) == false)
                {
                    Directory.CreateDirectory(errorLogDirPath);
                }

                var errorLogFilePath = Path.Combine(errorLogDirPath, $"ErrorLog {DateTime.Now:yyyyMMdd-HHmmss}.txt");

                File.WriteAllText(errorLogFilePath, message);
            }
            catch
            {
                //  何もしない
            }
        }

        /// <summary>
        /// exeのあるディレクトリパスを返す
        /// </summary>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public static string GetExeDirPath()
        {
            var assembly = System.Reflection.Assembly.GetEntryAssembly();

            if (assembly == null) throw new InvalidOperationException("GetExeDirPath error.");

            var dirPath = Path.GetDirectoryName(assembly.Location);

            if (dirPath == null) throw new InvalidOperationException("GetExeDirPath error.");

            return dirPath;
        }

        /// <summary>
        /// Workingディレクトリパスを返す
        /// </summary>
        /// <returns></returns>
        public static string GetWorkingDirPath()
        {
            return Path.Combine(GetExeDirPath(), "Working");
        }

        /// <summary>
        /// 文字フォーマットを保存するパスを返す
        /// 存在しない場合、作成する
        /// </summary>
        /// <returns></returns>
        public static string GetMojiFormatDirPath()
        {
            try
            {
                var dirPath = Path.Combine(GetExeDirPath(), "MojiFormat");

                if(Directory.Exists(dirPath) == false)
                {
                    Directory.CreateDirectory(dirPath);
                }

                return dirPath;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("GetMojiFormatDirPath error.", ex);
            }
        }

        /// <summary>
        /// ディレクトリ内の画像ファイルパスを返す
        /// </summary>
        /// <param name="dirPath"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public static string GetImagePath(int imageNo, string dirPath)
        {
            try
            {
                var imageFileNamePrefix = $"Image{imageNo}.";
                foreach (var filePath in Directory.GetFiles(dirPath))
                {
                    var fileName = Path.GetFileName(filePath);
                    if (fileName.StartsWith(imageFileNamePrefix, StringComparison.OrdinalIgnoreCase)) return filePath;
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"GetWorkingDirImagePath imageNo{imageNo} error.", ex);
            }

            return string.Empty;
        }

        /// <summary>
        /// Workingディレクトリにある画像のパスを返す
        /// </summary>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public static string GetWorkingDirImagePath(int imageNo)
        {
            return GetImagePath(imageNo, GetWorkingDirPath());
        }

        /// <summary>
        /// 作業ディレクトリ内の画像を削除する
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        public static void DeleteWorkingDirImage(int imageNo, string? loadingTargetFilePath = null)
        {
            try
            {
                var imagePath = GetWorkingDirImagePath(imageNo);

                //  もしこれからロードする対象のファイルを消そうとしているのであれば、それを実行しない
                if (loadingTargetFilePath != null)
                {
                    if (loadingTargetFilePath.Contains(imagePath)) return;
                }

                File.Delete(imagePath);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"DeleteWorkingDirImage imageNo{imageNo} error.", ex);
            }
        }

        /// <summary>
        /// 作業ディレクトリ内の画像を削除する
        /// </summary>
        public static void DeleteAllWorkingDirImage(string? loadingTargetFilePath = null)
        {
            DeleteWorkingDirImage(1, loadingTargetFilePath);
            DeleteWorkingDirImage(2, loadingTargetFilePath);
        }

        /// <summary>
        /// 作業ディレクトリを初期化する
        /// </summary>
        public static void InitWorkingDirectory()
        {
            try
            {
                var workingDirPath = GetWorkingDirPath();

                if (Directory.Exists(workingDirPath))
                {
                    //  作業ディレクトリがある場合、中のファイルを削除する
                    foreach (var filePath in Directory.GetFiles(workingDirPath))
                    {
                        File.Delete(filePath);
                    }
                }
                else
                {
                    //  作業ディレクトリがない場合、作業ディレクトリを作成する
                    Directory.CreateDirectory(workingDirPath);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("InitWorkingDirectory error.", ex);
            }
        }

        /// <summary>
        /// 読み込んだ画像をWorkingディレクトリにコピーする
        /// </summary>
        /// <param name="sourceImageFilePath"></param>
        /// <exception cref="InvalidOperationException"></exception>
        public static void CopyImageToWorkingDirectory(int imageNo, string sourceImageFilePath)
        {
            try
            {
                var externsion = Path.GetExtension(sourceImageFilePath);

                var destImageFilePath = Path.Combine(GetWorkingDirPath(), $"Image{imageNo}{externsion}");

                //  同じファイルの場合は何もしない
                if(sourceImageFilePath == destImageFilePath) return;

                File.Copy(sourceImageFilePath, destImageFilePath, true);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("CopyImageToWorkingDirectory error.", ex);
            }
        }

        /// <summary>
        /// XML形式でファイルを書き出す
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="data"></param>
        /// <param name="filePath"></param>
        public static void WriteXMLData<T>(T data, string filePath)
        {
            using (var stream = new StreamWriter(filePath, false, new UTF8Encoding(false)))
            {
                XmlSerializer xmlSerializer = new XmlSerializer(typeof(T));
                xmlSerializer.Serialize(stream, data);
            }
        }

        /// <summary>
        /// XML形式のファイルを読み出す
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="filePath"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public static T ReadXMLData<T>(string filePath)
        {
            using (var stream = File.OpenRead(filePath))
            using (var reader = XmlReader.Create(stream, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersFromEntities = 0,
            }))
            {
                XmlSerializer xmlSerializer = new XmlSerializer(typeof(T));
                var obj = xmlSerializer.Deserialize(reader);

                if (obj == null) throw new InvalidOperationException("XML convert null error.");

                return (T)obj;
            }
        }

        /// <summary>
        /// バージョン化された複数ページ形式でプロジェクトを保存する。
        /// </summary>
        public static void WriteVersionedProject(string projectFilePath, ProjectDocument project, bool createBackup = false)
        {
            new VersionedProjectWriter().Write(projectFilePath, project, createBackup);
        }

        /// <summary>
        /// ページ単位のasset sourceから画像実体を取得して、バージョン化形式へ保存する。
        /// </summary>
        public static void WriteVersionedProject(string projectFilePath, ProjectDocument project, IProjectAssetSource? assetSource, bool createBackup = false)
        {
            new VersionedProjectWriter().Write(projectFilePath, project, assetSource, createBackup);
        }

        /// <summary>
        /// バージョン化された複数ページ形式からプロジェクトを読み込む。
        /// </summary>
        public static ProjectDocument ReadVersionedProject(string projectFilePath)
        {
            return new VersionedProjectReader().Read(projectFilePath);
        }

        /// <summary>
        /// バージョン化形式を読み込み、画像実体を指定されたasset sinkへ復元する。
        /// </summary>
        public static ProjectDocument ReadVersionedProject(string projectFilePath, IProjectAssetSink? assetSink)
        {
            return new VersionedProjectReader().Read(projectFilePath, assetSink);
        }

        /// <summary>
        /// プロジェクト形式を判定し、現行形式または旧形式を読み込む。
        /// 旧形式は1ページへ移行され、結果にwarningが付与される。
        /// </summary>
        public static ProjectReadResult ReadProject(string projectFilePath)
        {
            return new ProjectReader().ReadResult(projectFilePath);
        }

        /// <summary>
        /// プロジェクト形式を判定し、画像実体をasset sinkへ復元して読み込む。
        /// </summary>
        public static ProjectReadResult ReadProject(string projectFilePath, IProjectAssetSink? assetSink)
        {
            return new ProjectReader().ReadResult(projectFilePath, assetSink);
        }

        /// <summary>
        /// 文字フォーマットデータを出力する
        /// </summary>
        /// <param name="mojiData"></param>
        /// <param name="filePath"></param>
        public static void WriteMojiFormat(MojiData mojiData, string filePath)
        {
            try
            {
                WriteXMLData(mojiData, filePath);
            }
            catch (Exception ex)
            {
                var ioex = new InvalidOperationException("WriteMojiFormat error.", ex);
                ioex.Data.Add("MojiData", mojiData);
                throw ioex;
            }
        }

        /// <summary>
        /// 文字データをファイルに出力する
        /// </summary>
        /// <param name="mojiData"></param>
        /// <param name="dirPath"></param>
        public static void WriteMojiData(MojiData mojiData, string dirPath)
        {
            try
            {
                var filePath = Path.Combine(dirPath, $"MojiData{mojiData.Id}.xml");
                WriteXMLData(mojiData, filePath);
            }
            catch (Exception ex)
            {
                var ioex = new InvalidOperationException("WriteMojiData error.", ex);
                ioex.Data.Add("MojiData", mojiData);
                throw ioex;
            }
        }

        /// <summary>
        /// 文字データをファイルに出力する
        /// </summary>
        /// <param name="mojiDatas"></param>
        /// <param name="dirPath"></param>
        /// <exception cref="InvalidOperationException"></exception>
        public static void WriteMojiDatas(IEnumerable<MojiData> mojiDatas, string dirPath)
        {
            try
            {
                foreach (var mojiData in mojiDatas)
                {
                    WriteMojiData(mojiData, dirPath);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("WriteMojiDatasToWorkingDir error.", ex);
            }
        }

        /// <summary>
        /// 文字データを読み出す
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public static MojiData ReadMojiData(string filePath)
        {
            MojiData mojiData;

            try
            {
                mojiData = ReadXMLData<MojiData>(filePath);

                mojiData.RestoreFullTextNewLine();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"ReadMojiData {filePath} error.", ex);
            }

            return mojiData;
        }

        /// <summary>
        /// 文字データをディレクトリから読み出す
        /// </summary>
        /// <param name="dirPath"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public static List<MojiData> ReadMojiDatas(string dirPath)
        {
            List<MojiData> mojiDatas = new List<MojiData>();

            try
            {
                //  文字データを読み出す
                foreach (var filePath in Directory.GetFiles(dirPath, "MojiData*.xml", SearchOption.TopDirectoryOnly))
                {
                    var mojiData = ReadMojiData(filePath);

                    //  ID重複チェック
                    //  重複している場合、存在する最大のID+1の値に設定する
                    if (mojiDatas.Exists(x => x.Id == mojiData.Id))
                    {
                        mojiData.Id = mojiDatas.Max(x => x.Id) + 1;
                    }

                    mojiDatas.Add(mojiData);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("ReadMojiDatas error.", ex);
            }

            return mojiDatas;
        }

        /// <summary>
        /// 文字データを作業ディレクトリから読み出す
        /// </summary>
        /// <returns></returns>
        public static List<MojiData> ReadMojiDatasFromWorkingDir()
        {
            return ReadMojiDatas(GetWorkingDirPath());
        }

        /// <summary>
        /// 情報テキストファイルを書き込む
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        public static void WriteInfoTextToWorkingDir(string filePath)
        {
            try
            {
                StringBuilder infoText = new StringBuilder();

                infoText.AppendLine($"SoftwareName:MojiCollaTool");
                infoText.AppendLine($"SoftwareVersion:{System.Reflection.Assembly.GetExecutingAssembly().GetName().Version}");
                infoText.AppendLine($"TimeStamp:{DateTime.Now}");

                File.WriteAllText(filePath, infoText.ToString(), new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("WriteInfoTextToWorkingDir error.", ex);
            }
        }

        /// <summary>
        /// 作業ディレクトリをプロジェクトファイルとして書き込む
        /// </summary>
        /// <param name="projectFilePath"></param>
        /// <param name="mojiDatas"></param>
        /// <exception cref="InvalidOperationException"></exception>
        public static void WriteWorkingDirToProjectDataFile(
            string projectFilePath,
            IEnumerable<MojiData> mojiDatas,
            CanvasData canvasData,
            bool createBackup = false)
        {
            WriteWorkingDirToProjectDataFile(projectFilePath, mojiDatas, canvasData, GetWorkingDirPath(), createBackup);
        }

        /// <summary>
        /// 現在のWorkingディレクトリから画像を明示的にコピーして旧形式projectを保存する。
        /// </summary>
        public static void WriteWorkingDirToProjectDataFile(
            string projectFilePath,
            IEnumerable<MojiData> mojiDatas,
            CanvasData canvasData,
            string sourceWorkingDirectoryPath,
            bool createBackup = false)
        {
            if (string.IsNullOrWhiteSpace(projectFilePath)) throw new ArgumentException("Project path is required.", nameof(projectFilePath));
            if (mojiDatas == null) throw new ArgumentNullException(nameof(mojiDatas));
            if (canvasData == null) throw new ArgumentNullException(nameof(canvasData));
            if (string.IsNullOrWhiteSpace(sourceWorkingDirectoryPath)) throw new ArgumentException("Source Working directory is required.", nameof(sourceWorkingDirectoryPath));

            var fullProjectFilePath = Path.GetFullPath(projectFilePath);
            var projectDirectoryPath = Path.GetDirectoryName(fullProjectFilePath);
            if (string.IsNullOrEmpty(projectDirectoryPath)) throw new InvalidOperationException("Project directory could not be determined.");

            string? temporaryWorkingDirectoryPath = null;
            string? temporaryProjectFilePath = null;
            try
            {
                // 共有Workingディレクトリを入力にしない。毎回空の一時領域から全entryを生成する。
                temporaryWorkingDirectoryPath = CreateTemporaryDirectory(projectDirectoryPath, "mctzip-write");

                WriteInfoTextToWorkingDir(Path.Combine(temporaryWorkingDirectoryPath, "Info.txt"));

                WriteMojiDatas(mojiDatas, temporaryWorkingDirectoryPath);

                WriteCanvasData(canvasData, temporaryWorkingDirectoryPath);

                CopyCurrentImagesToSaveWorkspace(canvasData, sourceWorkingDirectoryPath, temporaryWorkingDirectoryPath);

                temporaryProjectFilePath = Path.Combine(projectDirectoryPath, $".{Path.GetFileName(fullProjectFilePath)}.{Guid.NewGuid():N}.tmp");
                ZipFile.CreateFromDirectory(temporaryWorkingDirectoryPath, temporaryProjectFilePath);

                // zipを閉じた後に、中央ディレクトリとXMLを読戻し検証する。
                using (var validatedProject = ReadProjectData(temporaryProjectFilePath))
                {
                }

                ReplaceProjectFile(temporaryProjectFilePath, fullProjectFilePath, createBackup);
                temporaryProjectFilePath = null;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("CompressWorkingDirToProjectDataFile error.", ex);
            }
            finally
            {
                DeleteTemporaryDirectory(temporaryWorkingDirectoryPath);
                DeleteTemporaryFile(temporaryProjectFilePath);
            }
        }

        /// <summary>
        /// プロジェクトファイルを読み出し作業ファイルに展開する
        /// </summary>
        /// <param name="projectFilePath"></param>
        /// <exception cref="InvalidOperationException"></exception>
        public static void ReadProjectDataToWorkingDir(string projectFilePath)
        {
            ReadProjectDataToWorkingDir(projectFilePath, GetWorkingDirPath());
        }

        /// <summary>
        /// プロジェクトファイルを一時領域で検証し、成功した場合だけWorkingディレクトリを置換する。
        /// </summary>
        public static void ReadProjectDataToWorkingDir(string projectFilePath, string workingDirPath)
        {
            using var projectData = ReadProjectData(projectFilePath);
            CommitProjectDataToWorkingDir(projectData, workingDirPath);
        }

        /// <summary>
        /// 旧単ページ形式を一時領域へ展開し、CanvasDataとMojiDataを検証済み状態で返す。
        /// </summary>
        public static LegacyProjectData ReadProjectData(string projectFilePath)
        {
            if (string.IsNullOrWhiteSpace(projectFilePath)) throw new ArgumentException("Project path is required.", nameof(projectFilePath));

            string? stagingDirectoryPath = null;
            try
            {
                if (!File.Exists(projectFilePath)) throw new FileNotFoundException("Project file was not found.", projectFilePath);

                stagingDirectoryPath = CreateTemporaryDirectory(Path.GetTempPath(), "mctzip-read");
                ExtractLegacyArchive(projectFilePath, stagingDirectoryPath);

                var canvasData = ReadCanvasData(stagingDirectoryPath);
                ValidateCanvasImages(canvasData, stagingDirectoryPath);
                var mojiDatas = ReadMojiDatas(stagingDirectoryPath);
                return new LegacyProjectData(stagingDirectoryPath, canvasData, mojiDatas);
            }
            catch (Exception ex)
            {
                DeleteTemporaryDirectory(stagingDirectoryPath);
                throw new InvalidOperationException("ReadProjectData error.", ex);
            }
        }

        /// <summary>
        /// 検証済みの一時展開結果をWorkingディレクトリへコミットする。
        /// </summary>
        public static void CommitProjectDataToWorkingDir(LegacyProjectData projectData, string workingDirPath)
        {
            if (projectData == null) throw new ArgumentNullException(nameof(projectData));
            if (string.IsNullOrWhiteSpace(workingDirPath)) throw new ArgumentException("Working directory is required.", nameof(workingDirPath));

            var fullWorkingDirPath = Path.GetFullPath(workingDirPath);
            var parentDirectoryPath = Path.GetDirectoryName(fullWorkingDirPath);
            if (string.IsNullOrEmpty(parentDirectoryPath)) throw new InvalidOperationException("Working directory parent could not be determined.");

            Directory.CreateDirectory(parentDirectoryPath);
            if (File.Exists(fullWorkingDirPath)) throw new InvalidOperationException("Working path is a file.");

            var commitDirectoryPath = CreateTemporaryDirectory(parentDirectoryPath, "mctzip-commit");
            var sourceStagingDirectoryPath = projectData.StagingDirectoryPath;
            var backupDirectoryPath = $"{fullWorkingDirPath}.backup-{Guid.NewGuid():N}";
            var hadExistingDirectory = Directory.Exists(fullWorkingDirPath);
            var movedExistingDirectory = false;
            var movedStagingDirectory = false;

            try
            {
                // 読込stageが別volumeでも動くよう、Workingと同じ親に一度コピーする。
                CopyDirectoryContents(projectData.StagingDirectoryPath, commitDirectoryPath);

                if (hadExistingDirectory)
                {
                    Directory.Move(fullWorkingDirPath, backupDirectoryPath);
                    movedExistingDirectory = true;
                }

                Directory.Move(commitDirectoryPath, fullWorkingDirPath);
                movedStagingDirectory = true;
                projectData.MarkCommitted();
                DeleteTemporaryDirectory(sourceStagingDirectoryPath);

                if (movedExistingDirectory)
                {
                    DeleteTemporaryDirectory(backupDirectoryPath);
                }
            }
            catch (Exception ex)
            {
                DeleteTemporaryDirectory(commitDirectoryPath);

                if (movedStagingDirectory && Directory.Exists(fullWorkingDirPath))
                {
                    DeleteTemporaryDirectory(fullWorkingDirPath);
                }

                if (movedExistingDirectory && !Directory.Exists(fullWorkingDirPath) && Directory.Exists(backupDirectoryPath))
                {
                    Directory.Move(backupDirectoryPath, fullWorkingDirPath);
                }

                throw new InvalidOperationException("CommitProjectDataToWorkingDir error.", ex);
            }
        }

        private static void CopyDirectoryContents(string sourceDirectoryPath, string destinationDirectoryPath)
        {
            foreach (var sourceFilePath in Directory.GetFiles(sourceDirectoryPath, "*", SearchOption.TopDirectoryOnly))
            {
                var destinationFilePath = Path.Combine(destinationDirectoryPath, Path.GetFileName(sourceFilePath));
                File.Copy(sourceFilePath, destinationFilePath, overwrite: false);
            }
        }

        private static void CopyCurrentImagesToSaveWorkspace(CanvasData canvasData, string sourceWorkingDirectoryPath, string saveDirectoryPath)
        {
            var image1Path = ResolveCurrentImagePath(1, canvasData.ImageData1, sourceWorkingDirectoryPath);
            var image2Path = ResolveCurrentImagePath(2, canvasData.ImageData2, sourceWorkingDirectoryPath);

            CopyImageToSaveWorkspace(image1Path, saveDirectoryPath);
            CopyImageToSaveWorkspace(image2Path, saveDirectoryPath);
        }

        private static string? ResolveCurrentImagePath(int imageNo, ImageData? imageData, string sourceWorkingDirectoryPath)
        {
            var imagePaths = Directory.Exists(sourceWorkingDirectoryPath)
                ? GetImagePaths(imageNo, sourceWorkingDirectoryPath)
                : new List<string>();

            if (imagePaths.Count > 1)
            {
                throw new InvalidDataException($"Image{imageNo} has multiple extensions in the Working directory.");
            }

            var hasImageData = imageData != null && !imageData.IsNullData();
            if (!hasImageData)
            {
                // CanvasDataが画像を参照していない場合、古いWorking画像は保存しない。
                return null;
            }

            if (imagePaths.Count == 0)
            {
                throw new InvalidDataException($"CanvasData references Image{imageNo}, but the image file is missing.");
            }

            return imagePaths[0];
        }

        private static void CopyImageToSaveWorkspace(string? sourceImagePath, string saveDirectoryPath)
        {
            if (sourceImagePath == null) return;

            var destinationImagePath = Path.Combine(saveDirectoryPath, Path.GetFileName(sourceImagePath));
            File.Copy(sourceImagePath, destinationImagePath, overwrite: false);
        }

        private static List<string> GetImagePaths(int imageNo, string directoryPath)
        {
            var prefix = $"Image{imageNo}.";
            return Directory.GetFiles(directoryPath, "*", SearchOption.TopDirectoryOnly)
                .Where(path => Path.GetFileName(path).StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        private static void ValidateCanvasImages(CanvasData canvasData, string projectDirectoryPath)
        {
            ValidateCanvasImage(1, canvasData.ImageData1, projectDirectoryPath);
            ValidateCanvasImage(2, canvasData.ImageData2, projectDirectoryPath);
        }

        private static void ValidateCanvasImage(int imageNo, ImageData? imageData, string projectDirectoryPath)
        {
            var imagePaths = GetImagePaths(imageNo, projectDirectoryPath);
            if (imagePaths.Count > 1)
            {
                throw new InvalidDataException($"Image{imageNo} has multiple extensions in the project archive.");
            }

            var hasImageData = imageData != null && !imageData.IsNullData();
            if (!hasImageData)
            {
                if (imagePaths.Count != 0)
                {
                    throw new InvalidDataException($"Project archive contains Image{imageNo} without matching CanvasData.");
                }

                return;
            }

            if (imageData!.OriginalWidth <= 0 || imageData.OriginalHeight <= 0 || imagePaths.Count != 1)
            {
                throw new InvalidDataException($"CanvasData and Image{imageNo} are inconsistent.");
            }

            ValidateDecodedImage(imageNo, imageData, imagePaths[0]);
        }

        private static void ValidateDecodedImage(int imageNo, ImageData imageData, string imagePath)
        {
            try
            {
                using var stream = File.OpenRead(imagePath);
                var decoder = BitmapDecoder.Create(
                    stream,
                    BitmapCreateOptions.PreservePixelFormat,
                    BitmapCacheOption.OnLoad);

                if (decoder.Frames.Count == 0 || decoder.Frames[0].PixelWidth != imageData.OriginalWidth || decoder.Frames[0].PixelHeight != imageData.OriginalHeight)
                {
                    throw new InvalidDataException($"Image{imageNo} dimensions do not match CanvasData.");
                }
            }
            catch (InvalidDataException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new InvalidDataException($"Image{imageNo} could not be decoded.", ex);
            }
        }

        private static string CreateTemporaryDirectory(string parentDirectoryPath, string purpose)
        {
            var directoryPath = Path.Combine(parentDirectoryPath, $".{purpose}-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directoryPath);
            return directoryPath;
        }

        private static void DeleteTemporaryDirectory(string? directoryPath)
        {
            if (string.IsNullOrEmpty(directoryPath)) return;

            try
            {
                if (Directory.Exists(directoryPath)) Directory.Delete(directoryPath, recursive: true);
            }
            catch
            {
                // cleanup failure must not hide the original persistence error
            }
        }

        private static void DeleteTemporaryFile(string? filePath)
        {
            try
            {
                if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath)) File.Delete(filePath);
            }
            catch
            {
                // cleanup failure must not hide the original persistence error
            }
        }

        private static void ReplaceProjectFile(string temporaryProjectFilePath, string projectFilePath, bool createBackup)
        {
            if (!File.Exists(projectFilePath))
            {
                File.Move(temporaryProjectFilePath, projectFilePath);
                return;
            }

            var backupFilePath = createBackup
                ? $"{projectFilePath}.backup-{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}"
                : null;

            try
            {
                File.Replace(temporaryProjectFilePath, projectFilePath, backupFilePath, ignoreMetadataErrors: true);
            }
            catch (PlatformNotSupportedException)
            {
                // 旧Windowsや異なるFSでReplaceが使えなくても、既存fileを先に消さない。
                File.Move(temporaryProjectFilePath, projectFilePath, overwrite: true);
            }
            catch (NotSupportedException)
            {
                File.Move(temporaryProjectFilePath, projectFilePath, overwrite: true);
            }
            catch (IOException)
            {
                // Replace失敗時も旧file削除は行わず、安全なoverwrite fallbackを試す。
                File.Move(temporaryProjectFilePath, projectFilePath, overwrite: true);
            }
        }

        private static void ExtractLegacyArchive(string projectFilePath, string stagingDirectoryPath)
        {
            using var archive = ZipFile.OpenRead(projectFilePath);
            if (archive.Entries.Count > MaxLegacyArchiveEntries)
            {
                throw new InvalidDataException("The project archive contains too many entries.");
            }

            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            long totalLength = 0;
            foreach (var entry in archive.Entries)
            {
                var entryName = entry.FullName.Replace('\\', '/');
                var segments = entryName.Split('/');
                if (segments.Length != 1 || string.IsNullOrEmpty(segments[0]) || segments[0] == "." || segments[0] == ".." || entryName.Contains(':'))
                {
                    throw new InvalidDataException($"Unsafe project archive entry: {entry.FullName}");
                }

                if (!names.Add(entryName))
                {
                    throw new InvalidDataException($"Duplicate project archive entry: {entry.FullName}");
                }

                if (entry.Length < 0 || entry.Length > MaxLegacyEntrySize || MaxLegacyArchiveSize - totalLength < entry.Length)
                {
                    throw new InvalidDataException("The project archive is too large.");
                }

                totalLength += entry.Length;
                var destinationPath = Path.Combine(stagingDirectoryPath, segments[0]);
                entry.ExtractToFile(destinationPath, overwrite: false);
            }

            if (!names.Contains("Info.txt") || !names.Contains("CanvasData.xml"))
            {
                throw new InvalidDataException("The legacy project archive is missing required entries.");
            }
        }

        /// <summary>
        /// キャンバスデータを書き込む
        /// </summary>
        /// <param name="canvasData"></param>
        /// <param name="dirPath"></param>
        /// <exception cref="InvalidOperationException"></exception>
        public static void WriteCanvasData(CanvasData canvasData, string dirPath)
        {
            try
            {
                var filePath = Path.Combine(dirPath, $"CanvasData.xml");
                WriteXMLData(canvasData, filePath);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("WriteCanvasData error.", ex);
            }
        }

        /// <summary>
        /// キャンバスデータを読み出す
        /// </summary>
        /// <param name="dirPath"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public static CanvasData ReadCanvasData(string dirPath)
        {
            try
            {
                var filePath = Path.Combine(dirPath, $"CanvasData.xml");
                return ReadXMLData<CanvasData>(filePath);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("ReadCanvasData error.", ex);
            }
        }

        /// <summary>
        /// 作業ディレクトリからキャンバスデータを読み出す
        /// </summary>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public static CanvasData ReadCanvasDataFromWorkingDir()
        {
            try
            {
                return ReadCanvasData(GetWorkingDirPath());
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("ReadCanvasDataFromWorkingDir error.", ex);
            }
        }
    }
}
