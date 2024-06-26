namespace Test;

public class CSharpB
{
    internal int v;

    internal static CSharpC StaticEmptyC0 = new CSharpC(11);

    internal CSharpC InstanceEmptyC0;

    public static CSharpC StaticEmptyC
    {
        get
        {
            return StaticEmptyC0;
        }
        set
        {
            StaticEmptyC0 = value;
        }
    }

    public CSharpC InstanceEmptyC
    {
        get
        {
            return InstanceEmptyC0;
        }

        set
        {
            InstanceEmptyC0 = value;
        }
    }

    public int V
    {
        get
        {
            return v;
        }
    }

    public CSharpB(int v)
    {
        this.v = v;
        InstanceEmptyC0 = new CSharpC(0);
    }
}

public class CSharpC
{
    internal int V0;

    public int V
    {
        get
        {
            return V0;
        }
        set
        {
            V0 = value;
        }
    }

    public CSharpC(int v)
    {
        V0 = v;
    }
}

