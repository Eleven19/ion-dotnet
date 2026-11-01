namespace Eleven19.IonDotnet.Internals
{
    internal interface IPrivateWriter : IIonWriter
    {
        bool IsFieldNameSet();

        int GetDepth();

        void WriteIonVersionMarker();
    }
}
