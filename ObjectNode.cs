using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace viewstate_decoder
{
    public class ObjectManager
    {
        private ObjectNode rootNode = new ObjectNode
        {
            Name = "ROOT"
        };
        private List<ObjectNode> Libraries = [];
        private Dictionary<int, ObjectNode> objectMap = new Dictionary<int, ObjectNode>();

        public ObjectNode GetNodeWithId(int id)
        {
            if(objectMap.TryGetValue(id, out ObjectNode value))
            {
                return value;
            }
            else
            {
                var node = new ObjectNode{Id = id, Type="Unresolved"};
                objectMap.Add(id, node);
                return node;
            }
        }

        public void AddSystemClass(ObjectNode systemClass){
            this.rootNode.AddMember(systemClass);
        }

        public void AddLibrary(ObjectNode library)
        {
            this.rootNode.AddMember(library);
            this.Libraries.Add(library);
        }

        public void AddClassToLibrary(int libId, ObjectNode clazz)
        {
            Libraries.Where(l => l.Id == libId).First().AddMember(clazz);
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append(JsonSerializer.Serialize(rootNode, new JsonSerializerOptions { WriteIndented = true }));
            return sb.ToString();
        }
    }

    public class ObjectNode
    {
        public int Id { get; set; }  = -1;
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Type { get; set; } = null;
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Name { get; set; } = null;
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public object? Value { get; set; } = null;
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public List<ObjectNode> Members { get; set; } = [];

        public ObjectNode() { }

        public void AddMember(ObjectNode member)
        {
            Members.Add(member);
        }

        public override string ToString()
        {
            return $"${Name} - {Type}";
        }
        public string ToString(int depth = 0)
        {
            var sb = new StringBuilder();
            var padding = new string(' ', depth * 4);
            if (Name != "")
                sb.AppendLine($"{padding}Name: {Name}");
            if (Type != "")
                sb.AppendLine($"{padding}Type: {Type}");
            if( Value != null)
            {
                switch (Value)
                {
                    case Byte[] b:
                        sb.AppendLine($"{padding}Value: {BitConverter.ToString(b).Replace("-", "")}");
                        break;
                    default:
                        sb.AppendLine($"{padding}Value: {Value}");
                        break;
                }
            }
                

            foreach (var member in Members)
            {
                sb.Append(member.ToString(depth + 1));
            }
            return sb.ToString();
        }
    }
}
