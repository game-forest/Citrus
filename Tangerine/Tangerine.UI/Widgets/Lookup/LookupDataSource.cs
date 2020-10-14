namespace Tangerine.UI
{
	public interface ILookupDataSource
	{
		void Fill(LookupWidget lookupWidget);
	}

	public class DelegateLookupDataSource : ILookupDataSource
	{
		public delegate void FillDelegate(LookupWidget lookupWidget);

		private readonly FillDelegate fill;

		public DelegateLookupDataSource(FillDelegate fill)
		{
			this.fill = fill;
		}

		public void Fill(LookupWidget lookupWidget) => fill.Invoke(lookupWidget);
	}
}
