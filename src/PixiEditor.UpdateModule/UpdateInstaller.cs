﻿using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace PixiEditor.UpdateModule;

public class UpdateInstaller
{
    public const string TargetDirectoryName = "UpdateFiles";

    public UpdateInstaller(string archiveFileName, string targetDirectory)
    {
        ArchiveFileName = archiveFileName;
        TargetDirectory = targetDirectory;
    }

    public static string UpdateFilesPath { get; set; } = Path.Join(UpdateDownloader.DownloadLocation, TargetDirectoryName);

    public string ArchiveFileName { get; set; }

    public string TargetDirectory { get; set; }

    public void Install(StringBuilder log)
    {
        var processes = Process.GetProcessesByName("PixiEditor.Desktop");
        log.AppendLine($"Found {processes.Length} PixiEditor processes running.");
        if (processes.Length > 0)
        {
            log.AppendLine("Killing PixiEditor processes...");
            processes[0].WaitForExit();
            log.AppendLine("Processes killed.");
        }
        
        log.AppendLine("Extracting files");
        
        ZipFile.ExtractToDirectory(ArchiveFileName, UpdateFilesPath, true);
        
        log.AppendLine("Files extracted");
        string dirWithFiles = Directory.GetDirectories(UpdateFilesPath)[0];
        log.AppendLine($"Copying files from {dirWithFiles} to {TargetDirectory}");

        try
        {
            CopyFilesToDestination(dirWithFiles, log);
        }
        catch (IOException ex)
        {
            log.AppendLine($"Error copying files: {ex.Message}. Retrying in 1 second...");
            System.Threading.Thread.Sleep(1000);
            CopyFilesToDestination(dirWithFiles, log);
        }

        log.AppendLine("Files copied");
        log.AppendLine("Deleting archive and update files");
        
        DeleteArchive();
    }

    private void DeleteArchive()
    {
        File.Delete(ArchiveFileName);
        Directory.Delete(UpdateFilesPath, true);
    }

    private void CopyFilesToDestination(string sourceDirectory, StringBuilder log)
    {
        int totalFiles = Directory.GetFiles(UpdateFilesPath, "*", SearchOption.AllDirectories).Length;
        log.AppendLine($"Found {totalFiles} files to copy.");

        string[] files = Directory.GetFiles(sourceDirectory);

        foreach (string file in files)
        {
            string targetFileName = Path.GetFileName(file);
            string targetFilePath = Path.Join(TargetDirectory, targetFileName);
            log.AppendLine($"Copying {file} to {targetFilePath}");
            File.Copy(file, targetFilePath, true);
        }

        CopySubDirectories(sourceDirectory, TargetDirectory, log);
    }

    private void CopySubDirectories(string originDirectory, string targetDirectory, StringBuilder log)
    {
        string[] subDirs = Directory.GetDirectories(originDirectory);
        log.AppendLine($"Found {subDirs.Length} subdirectories to copy.");
        if(subDirs.Length == 0) return;

        foreach (string subDir in subDirs)
        {
            string targetDirPath = Path.Join(targetDirectory, Path.GetFileName(subDir));
            
            log.AppendLine($"Copying {subDir} to {targetDirPath}");

            CopySubDirectories(subDir, targetDirPath, log);

            string[] files = Directory.GetFiles(subDir);

            if (!Directory.Exists(targetDirPath))
            {
                Directory.CreateDirectory(targetDirPath);
            }

            foreach (string file in files)
            {
                string targetFileName = Path.GetFileName(file);
                log.AppendLine($"Copying {file} to {Path.Join(targetDirPath, targetFileName)}");
                File.Copy(file, Path.Join(targetDirPath, targetFileName), true);
            }
        }
    }
}
