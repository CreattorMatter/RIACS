namespace ServicioReintegros.AssistCard.Configuracion
{
    public sealed class SearchOpciones
    {
        public string Endpoint { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public string KnowledgeBaseName { get; set; } = string.Empty;
        public string IndexName { get; set; } = string.Empty;

        /// <summary>
        /// Nombre del campo del índice que contiene el texto del documento (donde se busca y desde donde se lee el contenido).
        /// Default "content" (indexador clásico de Blob). En índices creados con "Import and vectorize data" suele ser "chunk".
        /// </summary>
        public string ContentField { get; set; } = "content";

        /// <summary>
        /// Nombre del campo del índice con el nombre/título del documento. Default "metadata_storage_name".
        /// Dejar vacío si el índice no tiene un campo de nombre (se omite del select).
        /// </summary>
        public string TitleField { get; set; } = "metadata_storage_name";

        /// <summary>
        /// Nombre del campo del índice con la ruta del documento. Default "metadata_storage_path".
        /// Dejar vacío si el índice no tiene un campo de ruta (se omite del select).
        /// </summary>
        public string PathField { get; set; } = "metadata_storage_path";
    }
}
