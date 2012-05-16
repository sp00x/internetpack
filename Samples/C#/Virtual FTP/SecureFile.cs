using System;
using System.IO;
using RemObjects.InternetPack.Core;

namespace RemObjects.InternetPack.Ftp.VirtualFtp
{

  public class SecureFile : FtpFile
  {
    public SecureFile(IFtpFolder aParent, string aName, SecureStorage aStorage) : base(aParent, aName)
    {
      base.Invalidate ();
      File.Delete(fSecureFileName);
    }

        throw new Exception("Error retrieving file from secure storage: file does not exist.");
      
      Stream lStream = fStorage.GetFile(SecureFileName, 0); 
      try
      {

        byte[] lBuffer = new byte[BUFFER_SIZE];
        
        int lBytesRead = lStream.Read(lBuffer, 0, BUFFER_SIZE);
        while (lBytesRead > 0)
        }
      }
      finally
      {
        lStream.Close();
      }

      //return new FileStream(SecureFileName,FileMode.Open, FileAccess.Read, FileShare.Read);
    }

    public override void CreateFile(Stream aStream)
        throw new Exception("Error adding file to secure storage: file already exist.");
      
      //Stream lStream = new FileStream(SecureFileName,FileMode.CreateNew, FileAccess.Write, FileShare.None);
      Stream lStream = fStorage.CreateFile(SecureFileName); 
      try
      {

        byte[] lBuffer = new byte[BUFFER_SIZE];
        
        int lBytesRead = aStream.Read(lBuffer, 0, BUFFER_SIZE);
        while (lBytesRead > 0)
        }
        /* ToDo: eliminate one call to receive by checking for lBytesRead == BUFFER_SIZE? */
        lStream.Close();
        Complete = true;
      }
      catch (ConnectionClosedException)
      {
        lStream.Close();
        Complete = true;
      }
      catch (Exception)
      {
        lStream.Close();
        File.Delete(SecureFileName);
        //lSession.CurrentFolder.DeleteFile(ea.FileName,lSession);
        throw;
      }

    }

}