namespace RestaurantPosWpf;

/// <summary>One row in the reservations list plus inline delete-confirmation state.</summary>
public sealed class OpsReservationListRowVm
{
    public OpsReservation Reservation { get; }
    public bool ShowDeleteConfirm { get; init; }

    public OpsReservationListRowVm(OpsReservation reservation, bool showDeleteConfirm = false)
    {
        Reservation = reservation;
        ShowDeleteConfirm = showDeleteConfirm;
    }
}
