namespace ServicioReintegros.AssistCard.Dominio.Entidades
{
    public enum EstadoConversacion
    {
        Menu = 0,
        AwaitingIdentifier = 1,
        ShowingReintegro = 2,
        AwaitingName = 3,
        MenuPrincipal = 4,
        OtrasConsultasMenu = 5,
        OtrasConsultasProblem = 6,
        SelectingReintegro = 7,
        ReintegroMenu = 8,
        ReintegroFinancialMenu = 9,
        ReintegroPaymentsMenu = 10,
        ReintegroPendingDocsSelect = 11,
        ReintegroPendingDocsAction = 12,
        ReintegroPendingDocsAskContinue = 13,
        ReintegroProblem = 14,
        ReintegroExit = 15,
        Ended = 16
    }
}
