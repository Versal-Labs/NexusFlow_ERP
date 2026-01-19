namespace NexusFlow.Web.Models
{
    public class UserName
    {
        public string Name { get; set; }
        public UserAge Age { get; set; }
        public string Address { get; set; }
    }

    public class UserAge
    {
        public int Age { get; set; }

        public string Gender { get; set; }

    }
}
