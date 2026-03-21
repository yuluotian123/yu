using Framework;

public class LevelProcedure : ProcedureBase
{
    IFsm<IProcedureModule> procedureOwner;

    protected internal override void OnEnter(IFsm<IProcedureModule> procedureOwner)
    {
        Debugger.Info("Enter LevelProcedure");
        this.procedureOwner = procedureOwner;

        

    }

    protected internal override void OnLeave(IFsm<IProcedureModule> procedureOwner, bool isShutdown)
    {
        base.OnLeave(procedureOwner, isShutdown);
    }

    protected internal override void OnProcess(IFsm<IProcedureModule> procedureOwner, double elapseSeconds, double realElapseSeconds)
    {

    }
}