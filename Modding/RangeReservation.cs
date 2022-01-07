using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GepBot.Modding
{
    public class ReservationComparer : IComparer<RangeReservation>
    {
        public bool Ascending;

        public ReservationComparer(bool ascending)
        {
            this.Ascending = ascending;
        }

        public int Compare(RangeReservation x, RangeReservation y)
        {
            return Ascending
                ? x.start.CompareTo(y.start)
                : y.start.CompareTo(x.start);
        }
    }

    public enum ReservationType
    {
        ItemOrStatus,
        PhotonView,
    }

    public class PendingReservation
    {
        public RangeReservation reservation;
        public ReservationType type;

        public PendingReservation(RangeReservation reservation, ReservationType type)
        {
            this.reservation = reservation;
            this.type = type;
        }
    }

    public struct RangeReservation
    {
        public RangeReservation(int start, int end, string name)
        {
            this.start = start;
            this.end = end;
            this.name = name;
        }

        public int start, end;
        public string name;
    }
}
