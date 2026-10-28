using System.Collections.Generic;
namespace FrontierAtlas
{
    public sealed partial class TerritorySimulation
    {
        private sealed class ContactFront {public readonly Queue<int> cells=new Queue<int>();public readonly HashSet<int> known=new HashSet<int>();}
        private readonly Dictionary<int,ContactFront> _contacts=new Dictionary<int,ContactFront>();private HashSet<int>[] _neighborNations;
        private void AddContact(int faction,int other,int cell)
        {
            if(faction<0||other<0||faction==other)return;int key=faction*FactionCount+other;
            if(!_contacts.TryGetValue(key,out var front)){front=new ContactFront();_contacts[key]=front;}
            if(front.known.Add(cell))front.cells.Enqueue(cell);_neighborNations[faction].Add(other);
        }
        private void RebuildContacts()
        {
            _contacts.Clear();_neighborNations=new HashSet<int>[FactionCount];for(int f=0;f<FactionCount;f++)_neighborNations[f]=new HashSet<int>();
            for(int f=0;f<FactionCount;f++)foreach(int cell in _frontiers[f])for(int d=0;d<4;d++){int n=World.Neighbor(cell,d);if(n>=0&&Owners[n]>=0&&Owners[n]!=f)AddContact(f,Owners[n],n);}
        }
        private void UpdateContacts(int faction,int cell)
        {
            if(_neighborNations==null)return;
            for(int d=0;d<4;d++)
            {
                int n=World.Neighbor(cell,d);if(n<0||Owners[n]<0||Owners[n]==faction)continue;
                AddContact(faction,Owners[n],n);AddContact(Owners[n],faction,cell);
            }
        }
        private int ContactCell(int f,int enemy)
        {
            if(enemy<0||TerritoryCounts[enemy]==0||!_contacts.TryGetValue(f*FactionCount+enemy,out var front))return -1;
            while(front.cells.Count>0){int cell=front.cells.Peek();if(Owners[cell]==enemy&&!IsImpassable(cell)&&IsAdjacent(cell,f))return cell;front.cells.Dequeue();front.known.Remove(cell);}return -1;
        }
    }
}
