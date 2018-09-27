

namespace mw
{


	public partial class TFloat : global::ProtoBuf.IExtensible
	{
		public float ToFloat()
		{
			return v / 1000.0f;
		}
	}
	
	public partial class TTFloat : global::ProtoBuf.IExtensible
	{
		public float ToFloat()
		{
			return v / 1000000.0f;
		}
	}

}


