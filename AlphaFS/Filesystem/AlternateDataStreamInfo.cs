/* Copyright (C) 2008-2015 Peter Palotas, Jeffrey Jangli, Alexandr Normuradov
 *  
 *  Permission is hereby granted, free of charge, to any person obtaining a copy 
 *  of this software and associated documentation files (the "Software"), to deal 
 *  in the Software without restriction, including without limitation the rights 
 *  to use, copy, modify, merge, publish, distribute, sublicense, and/or sell 
 *  copies of the Software, and to permit persons to whom the Software is 
 *  furnished to do so, subject to the following conditions:
 *  
 *  The above copyright notice and this permission notice shall be included in 
 *  all copies or substantial portions of the Software.
 *  
 *  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR 
 *  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
 *  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE 
 *  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER 
 *  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, 
 *  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN 
 *  THE SOFTWARE. 
 */

using Alphaleonis.Win32.Security;
using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.AccessControl;

namespace Alphaleonis.Win32.Filesystem
{
   /// <summary>Provides properties and instance methods for the enumeration,
   /// creation and deletion of NTFS Alternate Data Streams.
   /// </summary>
   /// <remarks>This class cannot be inherited.</remarks>
   [SerializableAttribute]
   public sealed class AlternateDataStreamInfo
   {
      #region Private Fields
      
      private readonly PathFormat _pathFormat;
      private string _fullName;
      private string _longFullName;

      [NonSerialized]
      private KernelTransaction _transaction;

      #endregion

      #region Constructors

      /// <summary>Initializes a new instance of the <see cref="AlternateDataStreamInfo"/> class.</summary>
      /// <param name="path">The path to an existing file or directory.</param>
      public AlternateDataStreamInfo(string path) 
         : this(new NativeMethods.Win32StreamId(), null, path, null, null, null, PathFormat.RelativeOrFullPath)
      {
      }

      /// <summary>Initializes a new instance of the <see cref="AlternateDataStreamInfo"/> class.</summary>
      /// <param name="transaction">The transaction.</param>
      /// <param name="path">The path to an existing file or directory.</param>
      public AlternateDataStreamInfo(KernelTransaction transaction, string path)
         : this(new NativeMethods.Win32StreamId(), transaction, path, null, null, null, PathFormat.RelativeOrFullPath)
      {
      }

      /// <summary>Initializes a new instance of the <see cref="AlternateDataStreamInfo"/> class.</summary>
      /// <param name="handle">A <see cref="SafeFileHandle"/> connected to the file or directory from which to retrieve the information.</param>
      public AlternateDataStreamInfo(SafeFileHandle handle)
         : this(new NativeMethods.Win32StreamId(), null, Path.GetFinalPathNameByHandleInternal(handle, FinalPathFormats.None), null, null, null, PathFormat.RelativeOrFullPath)
      {
      }

      /// <summary>Initializes a new instance of the <see cref="AlternateDataStreamInfo"/> class.</summary>
      /// <param name="stream">The NativeMethods.Win32StreamId stream ID.</param>
      /// <param name="transaction"></param>
      /// <param name="path">The path to an existing file or directory.</param>
      /// <param name="name">The originalName of the stream.</param>
      /// <param name="originalName">The alternative data stream name originally specified by the user.</param>
      /// <param name="isFolder">Specifies that <paramref name="path"/> is a file or directory. <see langword="null"/> to retrieve automatically.</param>
      /// <param name="pathFormat">Indicates the format of the path parameter(s).</param>
      private AlternateDataStreamInfo(NativeMethods.Win32StreamId stream, KernelTransaction transaction, string path, string name, string originalName, bool? isFolder, PathFormat pathFormat)
      {
         if (Utils.IsNullOrWhiteSpace(path))
            throw new ArgumentNullException("path");

         _pathFormat = pathFormat;

         Transaction = transaction;

         OriginalName = originalName;
         Name = name ?? string.Empty;

         Attributes = stream.StreamAttributes;
         Type = stream.StreamType;
         Size = (long) stream.StreamSize;

         FullName = path + Name;

         if (isFolder == null)
         {
            FileAttributes attrs = File.GetAttributesExInternal<FileAttributes>(transaction, LongFullName, PathFormat.LongFullPath);
            IsDirectory = (attrs & FileAttributes.Directory) == FileAttributes.Directory;
         }
         else
            IsDirectory = (bool) isFolder;
      }

      #endregion // Constructors

      #region Properties


      /// <summary>The attributes of the data to facilitate cross-operating system transfer.</summary>
      public StreamAttributes Attributes { get; private set; }


      /// <summary>The source path of the stream.</summary>
      public string FullName
      {
         get { return _fullName; }

         private set
         {
            LongFullName = value;
            _fullName = Path.GetRegularPathInternal(LongFullName, false, false, false, false);
         }
      }



      /// <summary>The type of the data in the stream.</summary>
      [SuppressMessage("Microsoft.Naming", "CA1721:PropertyNamesShouldNotMatchGetMethods")]
      public StreamType Type { get; private set; }



      /// <summary>Gets a value indicating whether this instance represents a directory.</summary>
      /// <value><see langword="true"/> if this instance represents a directory; otherwise, <see langword="false"/>.</value>
      public bool IsDirectory { get; private set; }

      /// <summary>The full path of the file system object in Unicode (LongPath) format.</summary>
      internal string LongFullName
      {
         get { return _longFullName; }

         private set
         {
            _longFullName = _pathFormat == PathFormat.LongFullPath ? value : Path.GetLongPathInternal(value, GetFullPathOptions.None);
         }
      }

      /// <summary>The name of the alternative data stream.</summary>
      public string Name { get; private set; }

      /// <summary>The alternative data stream name originally specified by the user.</summary>
      private string OriginalName { get; set; }

      /// <summary>The size of the data in the substream, in bytes.</summary>
      public long Size { get; private set; }

      /// <summary>[AlphaFS] Represents the KernelTransaction that was passed to the constructor.</summary>
      public KernelTransaction Transaction
      {
         get { return _transaction; }
         private set { _transaction = value; }
      }

      #endregion // Properties

      #region Methods

      /// <summary>[AlphaFS] Adds an alternate data stream (NTFS ADS) to an existing file or directory.</summary>
      /// <param name="name">The name for the stream. If a stream with <paramref name="name"/> already exists, it will be overwritten.</param>
      /// <param name="contents">The lines to add to the stream.</param>      
      [SecurityCritical]
      public void AddStream(string name, string[] contents)
      {
         AddStreamInternal(IsDirectory, Transaction, LongFullName, name, contents, PathFormat.LongFullPath);
      }

      /// <summary>
      ///   [AlphaFS] Returns an enumerable collection of <see cref="AlternateDataStreamInfo"/>
      ///   instances for the file or directory.
      /// </summary>
      /// <returns>
      ///   An enumerable collection of <see cref="AlternateDataStreamInfo"/> instances for the file or
      ///   directory.
      /// </returns>
      [SecurityCritical]
      public IEnumerable<AlternateDataStreamInfo> EnumerateStreams()
      {
         return EnumerateStreamsInternal(IsDirectory, Transaction, null, LongFullName, null, null, PathFormat.LongFullPath);
      }
      /// <summary>
      ///   [AlphaFS] Returns an enumerable collection of <see cref="AlternateDataStreamInfo"/> of type
      ///   <see cref="StreamType"/> instances for the file or directory.
      /// </summary>
      /// <param name="streamType">The type of stream to retrieve.</param>
      /// <returns>
      ///   An enumerable collection of <see cref="AlternateDataStreamInfo"/> of type
      ///   <see cref="StreamType"/> instances for the file or directory.
      /// </returns>
      [SecurityCritical]
      public IEnumerable<AlternateDataStreamInfo> EnumerateStreams(StreamType streamType)
      {
         return EnumerateStreamsInternal(IsDirectory, Transaction, null, LongFullName, null, streamType, PathFormat.LongFullPath);
      }

      /// <summary>[AlphaFS] Removes all alternate data streams (NTFS ADS) from an existing file or directory.</summary>
      /// <remarks>This method only removes streams of type <see cref="StreamType.AlternateData"/>.
      /// No Exception is thrown if the stream does not exist.</remarks>
      [SecurityCritical]
      public void RemoveStream()
      {
         RemoveStreamInternal(IsDirectory, Transaction, LongFullName, null, PathFormat.LongFullPath);
      }

      /// <summary>[AlphaFS] Removes an alternate data stream (NTFS ADS) from an existing file or directory.</summary>
      /// <param name="name">The name of the stream to remove.</param>
      /// <remarks>This method only removes streams of type <see cref="StreamType.AlternateData"/>.
      /// No Exception is thrown if the stream does not exist.</remarks>
      [SecurityCritical]
      public void RemoveStream(string name)
      {
         RemoveStreamInternal(IsDirectory, Transaction, LongFullName, name, PathFormat.LongFullPath);
      }

      #region Unified Internals


      /// <summary>[AlphaFS] Unified method AddStreamInternal() to add an alternate data stream (NTFS ADS) to an existing file or directory.</summary>
      /// <param name="isFolder">Specifies that <paramref name="path"/> is a file or directory. <see langword="null"/> to retrieve automatically.</param>
      /// <param name="transaction">The transaction.</param>
      /// <param name="path">The path to an existing file or directory.</param>
      /// <param name="name">The name for the stream. If a stream with <paramref name="name"/> already exists, it will be overwritten.</param>
      /// <param name="contents">The lines to add to the stream.</param>
      /// <param name="pathFormat">Indicates the format of the path parameter(s).</param>      
      [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "StreamWriter() disposes of FileStream() object.")]
      [SecurityCritical]
      internal static void AddStreamInternal(bool isFolder, KernelTransaction transaction, string path, string name, IEnumerable<string> contents, PathFormat pathFormat)
      {
         if (Utils.IsNullOrWhiteSpace(path))
            throw new ArgumentNullException("path");

         if (Utils.IsNullOrWhiteSpace(name))
            throw new ArgumentNullException("name");

         if (name.Contains(Path.StreamSeparator))
            throw new ArgumentException(Resources.StreamNameWithColon);

         using (SafeFileHandle handle = File.CreateFileInternal(transaction, path + Path.StreamSeparator + name, isFolder ? ExtendedFileAttributes.BackupSemantics : ExtendedFileAttributes.Normal, null, FileMode.Create, FileSystemRights.Write, FileShare.ReadWrite, false, pathFormat))
         using (StreamWriter writer = new StreamWriter(new FileStream(handle, FileAccess.Write), NativeMethods.DefaultFileEncoding))
            foreach (string line in contents)
               writer.WriteLine(line);
      }

      
      /// <summary>
      ///   Unified method EnumerateStreamsInternal() to return an enumerable collection of
      ///   <see cref="AlternateDataStreamInfo"/> instances, associated with a file or directory.
      /// </summary>
      /// <param name="isFolder">
      ///   Specifies that <paramref name="path"/> is a file or directory. <see langword="null"/> to retrieve
      ///   automatically.
      /// </param>
      /// <param name="transaction">The transaction.</param>
      /// <param name="safeHandle">
      ///   A <see cref="SafeFileHandle"/> connected to the open file from which to retrieve the
      ///   information. Use either <paramref name="safeHandle"/> or <paramref name="path"/>, not both.
      /// </param>
      /// <param name="path">
      ///   The path to an existing file or directory. Use either <paramref name="path"/> or
      ///   <paramref name="safeHandle"/>, not both.
      /// </param>
      /// <param name="originalName">The name of the stream to retrieve.</param>
      /// <param name="streamType">The type of stream to retrieve.</param>
      /// <param name="pathFormat">Indicates the format of the path parameter(s).</param>
      /// <returns>
      ///   An <see cref="IEnumerable{AlternateDataStreamInfo}"/> collection of streams for the file or
      ///   directory specified by path.
      /// </returns>
      [SecurityCritical]
      internal static IEnumerable<AlternateDataStreamInfo> EnumerateStreamsInternal(bool? isFolder, KernelTransaction transaction, SafeFileHandle safeHandle, string path, string originalName, StreamType? streamType, PathFormat pathFormat)
      {
         string pathLp = null;

         bool callerHandle = safeHandle != null;
         if (!callerHandle)
         {
            pathLp = Path.GetExtendedLengthPathInternal(transaction, path, pathFormat, GetFullPathOptions.RemoveTrailingDirectorySeparator | GetFullPathOptions.CheckInvalidPathChars);

            if (isFolder == null)
            {
               FileAttributes attrs = File.GetAttributesExInternal<FileAttributes>(transaction, pathLp, PathFormat.LongFullPath);
               isFolder = (attrs & FileAttributes.Directory) == FileAttributes.Directory;
            }

            safeHandle = File.CreateFileInternal(transaction, pathLp,
               (bool) isFolder
                  ? ExtendedFileAttributes.BackupSemantics
                  : ExtendedFileAttributes.Normal, null,
               FileMode.Open, FileSystemRights.Read, FileShare.ReadWrite, false, PathFormat.LongFullPath);
         }
         else
            NativeMethods.IsValidHandle(safeHandle);


         try
         {
            using (new PrivilegeEnabler(Privilege.Backup))
            using (SafeGlobalMemoryBufferHandle safeBuffer = new SafeGlobalMemoryBufferHandle(NativeMethods.DefaultFileBufferSize))
            {
               Type typeWin32Stream = typeof(NativeMethods.Win32StreamId);
               uint sizeOfType = (uint) Marshal.SizeOf(typeWin32Stream);
               uint numberOfBytesRead;
               IntPtr context;

               bool doLoop = true;
               while (doLoop)
               {
                  if (!NativeMethods.BackupRead(safeHandle, safeBuffer, sizeOfType, out numberOfBytesRead, false, true, out context))
                     // Throws IOException.
                     NativeError.ThrowException(Marshal.GetLastWin32Error(), pathLp, true);

                  doLoop = numberOfBytesRead == sizeOfType;
                  if (doLoop)
                  {
                     string streamName = null;
                     string streamSearchName = null;

                     
                     // CA2001:AvoidCallingProblematicMethods

                     IntPtr buffer = IntPtr.Zero;
                     bool successRef = false;
                     safeBuffer.DangerousAddRef(ref successRef);

                     // MSDN: The DangerousGetHandle method poses a security risk because it can return a handle that is not valid.
                     if (successRef)
                        buffer = safeBuffer.DangerousGetHandle();

                     safeBuffer.DangerousRelease();

                     if (buffer == IntPtr.Zero)
                        NativeError.ThrowException(Resources.HandleDangerousRef);

                     // CA2001:AvoidCallingProblematicMethods


                     NativeMethods.Win32StreamId stream = Utils.MarshalPtrToStructure<NativeMethods.Win32StreamId>(0, buffer);

                     if (streamType == null || stream.StreamType == streamType)
                     {
                        if (stream.StreamNameSize > 0)
                        {
                           if (!NativeMethods.BackupRead(safeHandle, safeBuffer, stream.StreamNameSize, out numberOfBytesRead, false, true, out context))
                              // Throws IOException.
                              NativeError.ThrowException(Marshal.GetLastWin32Error(), pathLp, true);


                           // CA2001:AvoidCallingProblematicMethods

                           buffer = IntPtr.Zero;
                           successRef = false;
                           safeBuffer.DangerousAddRef(ref successRef);

                           // MSDN: The DangerousGetHandle method poses a security risk because it can return a handle that is not valid.
                           if (successRef)
                              buffer = safeBuffer.DangerousGetHandle();

                           safeBuffer.DangerousRelease();

                           if (buffer == IntPtr.Zero)
                              NativeError.ThrowException(Resources.HandleDangerousRef);

                           // CA2001:AvoidCallingProblematicMethods


                           streamName = Marshal.PtrToStringUni(buffer, (int)numberOfBytesRead / 2);

                           // Returned stream name format: ":streamName:$DATA"
                           streamSearchName = streamName.TrimStart(Path.StreamSeparatorChar).Replace(Path.StreamSeparator + "$DATA", string.Empty);
                        }

                        if (originalName == null || (streamSearchName != null && streamSearchName.Equals(originalName, StringComparison.OrdinalIgnoreCase)))
                           yield return new AlternateDataStreamInfo(stream, transaction, pathLp ?? path, streamName, originalName ?? streamSearchName, isFolder, pathFormat);
                     }

                     uint lo, hi;
                     doLoop = !NativeMethods.BackupSeek(safeHandle, uint.MinValue, uint.MaxValue, out lo, out hi, out context);
                  }
               }

               // MSDN: To release the memory used by the data structure,
               // call BackupRead with the bAbort parameter set to TRUE when the backup operation is complete.
               if (!NativeMethods.BackupRead(safeHandle, safeBuffer, 0, out numberOfBytesRead, true, false, out context))
                  // Throws IOException.
                  NativeError.ThrowException(Marshal.GetLastWin32Error(), pathLp, true);
            }
         }
         finally
         {
            // Handle is ours, dispose.
            if (!callerHandle && safeHandle != null)
               safeHandle.Close();
         }
      }



      /// <summary>Retrieves the actual number of bytes of disk storage used by all or a specific alternate data streams (NTFS ADS).</summary>
      /// <param name="isFolder">Specifies that <paramref name="path"/> is a file or directory. <see langword="null"/> to retrieve automatically.</param>
      /// <param name="transaction">The transaction.</param>
      /// <param name="handle">A <see cref="SafeFileHandle"/> connected to the open file from which to retrieve the information. Use either <paramref name="handle"/> or <paramref name="path"/>, not both.</param>
      /// <param name="path">A path that describes a file. Use either <paramref name="path"/> or <paramref name="handle"/>, not both.</param>
      /// <param name="name">The name of the stream to retrieve or <see langword="null"/> to retrieve all streams.</param>
      /// <param name="streamType">The type of stream to retrieve or <see langword="null"/> to retrieve all streams.</param>
      /// <param name="pathFormat">Indicates the format of the path parameter(s).</param>
      /// <returns>The actual number of bytes of disk storage used by all or a specific alternate data streams (NTFS ADS).</returns>
      [SecurityCritical]
      public static long GetStreamSizeInternal(bool? isFolder, KernelTransaction transaction, SafeFileHandle handle, string path, string name, StreamType? streamType, PathFormat pathFormat)
      {
         IEnumerable<AlternateDataStreamInfo> streamSizes = EnumerateStreamsInternal(isFolder, transaction, handle, path, name, streamType, pathFormat);

         return streamType == null
            ? streamSizes.Sum(stream => stream.Size)
            : streamSizes.Where(stream => stream.Type == streamType).Sum(stream => stream.Size);
      }



      /// <summary>Unified method RemoveStreamInternal() to remove alternate data streams (NTFS ADS) from a file or directory.</summary>
      /// <param name="isFolder">Specifies that <paramref name="path"/> is a file or directory. <see langword="null"/> to retrieve automatically.</param>
      /// <param name="transaction">The transaction.</param>
      /// <param name="path">The path to an existing file or directory.</param>
      /// <param name="name">The name of the stream to remove. When <see langword="null"/> all ADS are removed.</param>
      /// <param name="pathFormat">Indicates the format of the path parameter(s).</param>
      /// <remarks>This method only removes streams of type <see cref="StreamType.AlternateData"/>.
      /// No Exception is thrown if the stream does not exist.</remarks>      
      [SecurityCritical]
      internal static void RemoveStreamInternal(bool? isFolder, KernelTransaction transaction, string path, string name, PathFormat pathFormat)
      {

         string pathLp = Path.GetExtendedLengthPathInternal(transaction, path, pathFormat, GetFullPathOptions.RemoveTrailingDirectorySeparator | GetFullPathOptions.CheckInvalidPathChars);

         foreach (AlternateDataStreamInfo stream in EnumerateStreamsInternal(isFolder, transaction, null, pathLp, name, StreamType.AlternateData, PathFormat.LongFullPath))
            File.DeleteFileInternal(transaction, string.Format(CultureInfo.CurrentCulture, "{0}{1}{2}{1}$DATA", pathLp, Path.StreamSeparator, stream.OriginalName), false, PathFormat.LongFullPath);
      }


      #endregion 

      #endregion Methods

   }
}