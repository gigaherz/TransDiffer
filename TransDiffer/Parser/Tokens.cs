
namespace TransDiffer.Parser
{
    public enum Tokens
    {
        Comma,
        Colon,
        LBrace,
        RBrace,
        LParen,
        RParen,

        Plus,
        Minus,
        Asterisk,
        Slash,
        Pipe,
        Ampersand,
        Squiggly,

        HexInt,
        Integer,
        Double,
        String,

        Ident,

        And,
        Or,
        Not,

        Begin,
        IdEnd,
        Language,
        Menu,
        MenuEx,
        Popup,
        MenuItem,
        Separator,
        Dialog,
        DialogEx,
        Style,
        Caption,
        Font,
        Control,
        EditText,
        DefPushButton,
        PushButton,
        LText,
        RText,
        GroupBox,
        ListBox,
        CheckBox,
        RadioButton,
        StringTable,
        Discardable,
        AutoRadioButton,
        AutoCheckBox,
        Icon,
        ComboBox,
        CText,

        End
    }
}
