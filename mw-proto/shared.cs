//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

// Generated from: shared.proto
namespace mw
{
  [global::System.Serializable, global::ProtoBuf.ProtoContract(Name=@"Enums")]
  public partial class Enums : global::ProtoBuf.IExtensible
  {
    public Enums() {}
    
    //private global::ProtoBuf.IExtension extensionObject;
    global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
      { 
      // modified by zeta.
      //return global::ProtoBuf.Extensible.GetExtensionObject(ref extensionObject, createIfMissing);
      return null;
      }
  }
  
  [global::System.Serializable, global::ProtoBuf.ProtoContract(Name=@"TFloat")]
  public partial class TFloat : global::ProtoBuf.IExtensible
  {
    public TFloat() {}
    
    private int _v;
    [global::ProtoBuf.ProtoMember(1, IsRequired = true, Name=@"v", DataFormat = global::ProtoBuf.DataFormat.TwosComplement)]
    public int v
    {
      get { return _v; }
      set { _v = value; }
    }
    //private global::ProtoBuf.IExtension extensionObject;
    global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
      { 
      // modified by zeta.
      //return global::ProtoBuf.Extensible.GetExtensionObject(ref extensionObject, createIfMissing);
      return null;
      }
  }
  
  [global::System.Serializable, global::ProtoBuf.ProtoContract(Name=@"TTFloat")]
  public partial class TTFloat : global::ProtoBuf.IExtensible
  {
    public TTFloat() {}
    
    private int _v;
    [global::ProtoBuf.ProtoMember(1, IsRequired = true, Name=@"v", DataFormat = global::ProtoBuf.DataFormat.TwosComplement)]
    public int v
    {
      get { return _v; }
      set { _v = value; }
    }
    //private global::ProtoBuf.IExtension extensionObject;
    global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
      { 
      // modified by zeta.
      //return global::ProtoBuf.Extensible.GetExtensionObject(ref extensionObject, createIfMissing);
      return null;
      }
  }
  
    [global::ProtoBuf.ProtoContract(Name=@"PkgFlag")]
    public enum PkgFlag
    {
            
      [global::ProtoBuf.ProtoEnum(Name=@"PKG_COMPRESS", Value=0)]
      PKG_COMPRESS = 0,
            
      [global::ProtoBuf.ProtoEnum(Name=@"PKG_CRYPTO", Value=1)]
      PKG_CRYPTO = 1
    }
  
}