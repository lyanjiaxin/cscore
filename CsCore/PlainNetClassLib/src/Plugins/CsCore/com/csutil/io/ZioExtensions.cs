using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Zio;
using Zio.FileSystems;

namespace com.csutil {
    public static class ZioExtensions {

        public static DirectoryEntry GetChildDir(this DirectoryEntry self, string subDirName) {
            return new DirectoryEntry(self.FileSystem, self.Path / subDirName);
        }

        public static FileEntry GetChild(this DirectoryEntry self, string fileName) {
            return new FileEntry(self.FileSystem, self.Path / fileName);
        }

        public static DirectoryEntry ToRootDirectoryEntry(this DirectoryInfo localDir) {
            var pfs = new PhysicalFileSystem();
            var fs = new SubFileSystem(pfs, pfs.ConvertPathFromInternal(localDir.FullName));
            return fs.GetDirectoryEntry(UPath.Root);
        }

        public static DirectoryEntry CreateV2(this DirectoryEntry self) {
            if (!self.Exists) { self.Create(); }
            return self;
        }

        public static bool OpenInExternalApp(this FileSystemEntry self) {
            try {
                System.Diagnostics.Process.Start(@self.FullName);
                return true;
            }
            catch (Exception e) { Log.e(e); }
            return false;
        }

        public static bool Rename(this DirectoryEntry self, string newName) {
            var target = self.Parent.GetChildDir(newName);
            AssertV2.IsFalse(target.IsNotNullAndExists(), "Already exists: target=" + target.FullName);
            return self.MoveToV2(target);
        }

        public static bool CopyTo(this DirectoryEntry source, DirectoryEntry target, bool replaceExisting = false) {
            AssertNotIdentical(source, target);
            if (!replaceExisting && target.IsNotNullAndExists()) {
                throw new ArgumentException("Cant copy to existing folder " + target);
            }
            foreach (var subDir in source.EnumerateDirectories()) {
                CopyTo(subDir, target.GetChildDir(subDir.FullName), replaceExisting);
            }
            foreach (var file in source.EnumerateFiles()) {
                target.CreateV2();
                var createdFile = file.CopyTo(target.GetChild(file.Name).FullName, replaceExisting);
                AssertV2.IsTrue(createdFile.Exists, "!createdFile.Exists: " + createdFile);
            }
            return target.Exists;
        }

        public static bool MoveToV2(this DirectoryEntry source, DirectoryEntry target) {
            AssertNotIdentical(source, target);
            var tempCopyId = "" + Guid.NewGuid();
            var originalPath = source.FullName;
            if (EnvironmentV2.isWebGL) {
                // In WebGL .MoveTo does not work correctly so copy+delete is tried instead:
                var tempDir = EnvironmentV2.instance.GetOrAddTempFolder("TmpCopies").GetChildDir(tempCopyId);
                source.CopyTo(tempDir);
            }
            source.MoveTo(target.FullName);
            if (EnvironmentV2.isWebGL) {
                if (!target.Exists) {
                    EmulateMoveViaCopyDelete(originalPath, tempCopyId, target);
                } else {
                    Log.e("WebGL TempCopy solution was not needed!");
                }
            }
            return target.Exists;
        }

        private static void AssertNotIdentical(DirectoryEntry source, DirectoryEntry target) {
            if (Equals(source.FullName, target.FullName)) {
                throw new OperationCanceledException("Identical source & target: " + source);
            }
        }

        /// <summary> Needed in WebGL because .MoveTo does not correctly move the files to
        /// the new target directory but instead only removes the original dir </summary>
        private static void EmulateMoveViaCopyDelete(string source, string tempDirPath, DirectoryEntry target) {
            var tempDir = EnvironmentV2.instance.GetOrAddTempFolder("TmpCopies").GetChildDir(tempDirPath);
            if (tempDir.IsEmtpy()) {
                target.CreateV2();
                CleanupAfterEmulatedMove(source, tempDir);
            } else if (tempDir.CopyTo(target)) {
                CleanupAfterEmulatedMove(source, tempDir);
            } else {
                Log.e("Could not move tempDir=" + tempDir + " into target=" + target);
            }
        }

        private static void CleanupAfterEmulatedMove(string source, DirectoryEntry tempDir) {
            tempDir.DeleteV2();
            var originalDir = new DirectoryInfo(source);
            try { if (originalDir.ExistsV2()) { originalDir.DeleteV2(); } } catch (Exception e) { Log.e("Cleanup err of original dir: " + originalDir, e); }
        }

        [Obsolete("Use .Name instead")]
        public static string NameV2(this FileSystemEntry self) { return self.Name; }

        public static bool IsNotNullAndExists(this FileSystemEntry self) {
            if (self == null) { return false; } else { return self.Exists; }
        }

        [Obsolete("Use .Exists instead")]
        public static bool ExistsV2(this FileSystemEntry self) { return self.Exists; }

        /// <summary>
        /// Gets the remaining path after the <see cref="prefix"/>.
        /// </summary>
        /// <param name="prefix">The prefix of the path.</param>
        /// <param name="self">The path to search.</param>
        /// <returns>The path after the prefix, or a <c>null</c> path if <see cref="path"/> does not have the correct prefix.</returns>
        public static UPath RemovePrefix(this UPath self, UPath prefix) {
            if (prefix.IsEmpty) { throw new InvalidDataException("The passed prefix cant be emtpy, must minimum be the UPath.root"); }
            if (!self.IsInDirectory(prefix, true)) { throw new InvalidDataException($"Path '{self}' is not in '{prefix}'"); }
            var remaining = self.FullName.Substring(prefix.FullName.Length);
            return new UPath(remaining).ToAbsolute();
        }

        public static UPath AddPrefix(this UPath self, UPath prefix) {
            return prefix.IsNull ? self : prefix / self.ToRelative();
        }

        /// <summary>
        /// Gets the first directory of the specified path and return the remaining path (/a/b/c, first directory: /a, remaining: b/c)
        /// </summary>
        /// <param name="path">The path to extract the first directory and remaining.</param>
        /// <param name="remainingPath">The remaining relative path after the first directory</param>
        /// <returns>The first directory of the path.</returns>
        /// <exception cref="ArgumentNullException">if path is <see cref="UPath.IsNull"/></exception>
        public static UPath GetFirstPart(this UPath path) {
            path.AssertNotNull();
            var fullname = path.FullName;
            var index = fullname.IndexOf(UPath.DirectorySeparator, 1);
            if (index < 0) { return fullname; }
            return fullname.Substring(0, index);
        }

        public static bool DeleteV2(this FileSystemEntry self) {
            return DeleteV2(self, () => { self.Delete(); return true; });
        }

        public static bool DeleteV2(this DirectoryEntry self, bool deleteAlsoIfNotEmpty = true) {
            return DeleteV2(self, () => {
                if (deleteAlsoIfNotEmpty) { // Recursively delete all children first:
                    if (!self.IsEmtpy()) {
                        foreach (var subDir in self.GetDirectories()) { subDir.DeleteV2(deleteAlsoIfNotEmpty); }
                        foreach (var file in self.GetFiles()) { file.DeleteV2(); }
                    }
                }
                if (!self.IsEmtpy()) { throw new IOException("Cant delete non-emtpy dir: " + self.FullName); }
                try { self.Delete(); return true; } catch (Exception e) { Log.e(e); }
                return false;
            });
        }

        public static IEnumerable<DirectoryEntry> GetDirectories(this DirectoryEntry self) { return self.EnumerateDirectories(); }

        public static IEnumerable<FileEntry> GetFiles(this DirectoryEntry self) { return self.EnumerateFiles(); }

        private static bool DeleteV2(FileSystemEntry self, Func<bool> deleteAction) {
            if (self.IsNotNullAndExists()) {
                var res = deleteAction();
                AssertV2.IsFalse(!res || self.Exists, "Still exists: " + self.FullName);
                return res;
            }
            return false;
        }

        public static bool IsEmtpy(this DirectoryEntry self) { // TODO use old method to avoid exceptions?
            try { return !self.EnumerateEntries().Any(); } catch (Exception) { return true; }
        }

        public static long GetFileSize(this FileEntry self) { return self.Length; }

        public static string GetFileSizeString(this FileEntry self) {
            return ByteSizeToString.ByteSizeToReadableString(self.GetFileSize());
        }

        public static string GetNameWithoutExtension(this FileEntry self) {
            return Path.GetFileNameWithoutExtension(self.Name);
        }

    }
}