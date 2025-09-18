using Models;

namespace api.Atividade;



public class AtividadeDTO
    {
        public situacao situacao {get;set;} = situacao.proximo;
        public string categoria {get;set;} = "";
        public string descricao {get;set;} = "";
        public DateTime data_fim {get;set;}
    
    }

 