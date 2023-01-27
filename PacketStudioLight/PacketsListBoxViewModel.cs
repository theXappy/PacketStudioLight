using System;
using System.Collections.ObjectModel;
using System.Windows;
using GongSolutions.Wpf.DragDrop;

namespace PacketStudioLight;

class PacketsListBoxViewModel : IDropTarget
{
    public ObservableCollection<string> MyPacketsList { get; set; }

    public event EventHandler<PacketMovedEventArgs> Updated;

    public PacketsListBoxViewModel(ObservableCollection<string> myPacketsList)
    {
        MyPacketsList = myPacketsList;
    }

    void IDropTarget.DragOver(IDropInfo dropInfo)
    {
        dropInfo.DropTargetAdorner = DropTargetAdorners.Insert;
        dropInfo.Effects = DragDropEffects.Move;
    }

    void IDropTarget.Drop(IDropInfo dropInfo)
    {
        int toIndex = dropInfo.InsertIndex;
        int fromIndex = MyPacketsList.IndexOf(dropInfo.Data as string);
        if (toIndex > fromIndex)
        {
            // When removing the packet from the current index, it moves the dest index back by 1
            toIndex = toIndex - 1;
        }
        if (fromIndex == toIndex)
            return;
        // First let's move the UI elemenmts to show responsiveness to the user
        string textualItemToMove = MyPacketsList[fromIndex];
        MyPacketsList.RemoveAt(fromIndex);
        MyPacketsList.Insert(toIndex, textualItemToMove);

        // Finally ask the window to move pacets in the memory pcapng and  re-generate a packets list
        // (to, at least, fix indexes & timestamps. Possibly also the "info column" would be affected)
        Updated?.Invoke(this, new PacketMovedEventArgs(fromIndex, toIndex));
    }
}