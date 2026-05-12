namespace ServicioReintegros.AssistCard.Aplicacion.Servicios
{
    /// <summary>
    /// Centraliza TODOS los prompts enviados a la IA (Azure Foundry).
    /// Cada prompt es trilingüe y acepta variables interpoladas.
    /// Elimina los ~15 bloques de prompts inline del orquestador.
    /// </summary>
    public static class PromptTemplates
    {
        public static string RequisitosReintegro(string locale, string email)
            => LocaleHelper.Loc(locale,
                $"OBLIGATORIO: Buscá en la base de conocimiento los REQUISITOS Y PASOS para iniciar un reintegro desde la app My Assist Card. Respondé con los pasos oficiales. Si el problema no se resuelve, indicá que pueden escribir a {email}. Devolvé SOLO un objeto JSON con propiedad 'message'.",
                $"MANDATORY: Search the knowledge base for REQUIREMENTS AND STEPS to start a reimbursement from the My Assist Card app. Respond with the official steps. If the issue is not resolved, indicate they can write to {email}. Return ONLY a JSON object with a 'message' property.",
                $"OBRIGATÓRIO: Busque na base de conhecimento os REQUISITOS E PASSOS para iniciar um reembolso no app My Assist Card. Responda com os passos oficiais. Se o problema não se resolver, indique que podem escrever para {email}. Retorne APENAS um objeto JSON com propriedade 'message'.");

        public static string ProblemasApp(string locale, string email)
            => LocaleHelper.Loc(locale,
                $"OBLIGATORIO: El usuario reporta problemas con la App MyAC. DEBÉS buscar en la base de conocimiento, específicamente en la diapositiva de capacitación 'DUDAS SOBRE PROBLEMAS TECNICOS' (diapositiva 10), para brindar los pasos técnicos oficiales. Explicá: 1) Volver a intentar, 2) Actualizar o reinstalar la App, 3) Si el SO no descarga actualizaciones automáticas, desinstalar y reinstalar. Si el problema persiste, brindá el email {email} (NO 'soporte técnico'). Devolvé SOLO un objeto JSON con propiedad 'message'.",
                $"MANDATORY: The user is reporting problems with the MyAC App. You MUST search the knowledge base, specifically the training slide 'DOUBTS ABOUT TECHNICAL PROBLEMS' (slide 10), to provide the official technical steps. Explain: 1) Try again, 2) Update or reinstall the App, 3) If the OS doesn't auto-update, uninstall and reinstall. If the problem persists, provide the email {email} (NOT 'technical support'). Return ONLY a JSON object with a 'message' property.",
                $"OBRIGATÓRIO: O usuário está relatando problemas com o App MyAC. Você DEVE buscar na base de conhecimento, especificamente no slide de capacitação 'DÚVIDAS SOBRE PROBLEMAS TÉCNICOS' (slide 10), para fornecer os passos técnicos oficiais. Explique: 1) Tentar novamente, 2) Atualizar ou reinstalar o App, 3) Se o SO não atualiza automaticamente, desinstalar e reinstalar. Se o problema persistir, forneça o email {email} (NÃO 'suporte técnico'). Retorne APENAS um objeto JSON com propriedade 'message'.");

        public static string ReclamoDemoraPago(string locale, string email)
            => LocaleHelper.Loc(locale,
                $"OBLIGATORIO: El usuario reclama demora en el pago. DEBÉS buscar en la base de conocimiento, específicamente en el documento 'Agente de consultas sobre reintegros - Capacitación.pdf', página 9 ('DUDAS SOBRE PROCESO'), y responder acorde a los lineamientos. Reglas: 1) NO prometas acelerar el proceso. 2) Si está dentro de los primeros 20 días desde la carga (CreateDate), informá que la evaluación dura aprox. 30 días. 3) SIEMPRE brindá el email de contacto: {email}. Devolvé SOLO un objeto JSON con propiedad 'message'.",
                $"MANDATORY: The user is complaining about payment delays. You MUST search the knowledge base, specifically the document 'Agente de consultas sobre reintegros - Capacitación.pdf', page 9 ('PROCESS DOUBTS'), and answer accordingly. Rules: 1) Do NOT promise to accelerate the process. 2) If within the first 20 days since submission (CreateDate), explain evaluation takes around 30 days. 3) ALWAYS provide the contact email: {email}. Return ONLY a JSON object with a 'message' property.",
                $"OBRIGATÓRIO: O usuário reclama de demora no pagamento. Você DEVE buscar na base de conhecimento, especificamente no documento 'Agente de consultas sobre reintegros - Capacitación.pdf', página 9 ('DÚVIDAS SOBRE PROCESSO'), e responder conforme os lineamentos. Regras: 1) NÃO prometa acelerar o processo. 2) Se estiver dentro dos primeiros 20 dias desde a solicitação (CreateDate), informe que a avaliação leva cerca de 30 dias. 3) SEMPRE forneça o email de contato: {email}. Retorne APENAS um objeto JSON com propriedade 'message'.");

        public static string PlazosPago(string locale)
            => LocaleHelper.Loc(locale,
                "OBLIGATORIO: DEBÉS usar la herramienta Azure AI Search para buscar en la base de conocimiento la respuesta OFICIAL sobre plazos de pago. Buscá 'plazos de pago' o 'cuanto tiempo pagan'. NO respondas sin buscar primero. Una vez que encuentres el documento oficial, reproducilo EXACTAMENTE incluyendo todos los detalles (nombre, fechas, 7 días hábiles). Devolvé SOLO un objeto JSON con propiedad 'message' que contenga la respuesta oficial.",
                "MANDATORY: You MUST use the Azure AI Search tool to search the knowledge base for the OFFICIAL answer to the payment terms question. Search for 'payment terms' or 'cuanto tiempo pagan'. DO NOT answer without searching first. Once you find the official document, reproduce it EXACTLY including all details (name, dates, 7 business days). Return ONLY a JSON object with a 'message' property containing the official answer.",
                "OBRIGATÓRIO: Você DEVE usar a ferramenta Azure AI Search para buscar na base de conhecimento a resposta OFICIAL sobre prazos de pagamento. Busque por 'prazos de pagamento' ou 'cuanto tiempo pagan'. NÃO responda sem buscar primeiro. Ao encontrar o documento oficial, reproduza-o EXATAMENTE incluindo todos os detalhes (nome, datas, 7 dias úteis). Retorne APENAS um objeto JSON com propriedade 'message' contendo a resposta oficial.");

        public static string MonedaPago(string locale)
            => LocaleHelper.Loc(locale,
                "OBLIGATORIO: Buscá la Cláusula 4.4.2.7 REEMBOLSOS en la base de conocimiento. DEBÉS citar esta cláusula y explicar que ASSIST CARD reintegra en MONEDA LOCAL, NO en la moneda del gasto. El pago se realiza utilizando el cambio oficial tipo vendedor del día anterior al pago. NO digas que se pagará en USD o en la moneda del gasto. Devolvé SOLO un objeto JSON con propiedad 'message' que contenga la cita oficial de la cláusula y la explicación.",
                "MANDATORY: Search for Clause 4.4.2.7 REEMBOLSOS or REIMBURSEMENTS in the knowledge base. You MUST cite this clause and explain that ASSIST CARD reimburses in LOCAL CURRENCY, NOT in the currency of the expense. The payment uses the official exchange rate from the day before payment. DO NOT say it will be paid in USD or the expense currency. Return ONLY a JSON object with a 'message' property containing the official clause citation and explanation.",
                "OBRIGATÓRIO: Busque a Cláusula 4.4.2.7 REEMBOLSOS ou REIMBURSEMENTS na base de conhecimento. Você DEVE citar esta cláusula e explicar que a ASSIST CARD reembolsa em MOEDA LOCAL, NÃO na moeda da despesa. O pagamento usa a taxa de câmbio oficial do dia anterior ao pagamento. NÃO diga que será pago em USD ou na moeda da despesa. Retorne APENAS um objeto JSON com propriedade 'message' contendo a citação oficial da cláusula e explicação.");

        public static string TipoCambio(string locale)
            => LocaleHelper.Loc(locale,
                "OBLIGATORIO: El usuario pregunta sobre el tipo de cambio de su reintegro. DEBÉS explicar: 1) La solicitud fue por [monto] en [moneda original] (usá detalles de AuditResult), 2) El monto fue aprobado, 3) El pago se realizará en MONEDA LOCAL (NO en USD), 4) Utilizando el CAMBIO OFICIAL TIPO VENDEDOR del día anterior al pago. NO digas que el pago será en USD. Incluí los detalles del gasto original (concepto, monto, moneda). Devolvé SOLO un objeto JSON con propiedad 'message'.",
                "MANDATORY: The user is asking about the exchange rate for their reimbursement. You MUST explain: 1) The request was for [amount] in [original currency] (use AuditResult details), 2) The amount was approved, 3) Payment will be made in LOCAL CURRENCY (NOT USD), 4) Using the OFFICIAL EXCHANGE RATE from the day before payment. DO NOT say the payment will be in USD. Include the original expense details (concept, amount, currency). Return ONLY a JSON object with a 'message' property.",
                "OBRIGATÓRIO: O usuário está perguntando sobre a taxa de câmbio para seu reembolso. Você DEVE explicar: 1) A solicitação foi de [valor] em [moeda original] (use detalhes de AuditResult), 2) O valor foi aprovado, 3) O pagamento será feito em MOEDA LOCAL (NÃO USD), 4) Usando a TAXA DE CÂMBIO OFICIAL do dia anterior ao pagamento. NÃO diga que o pagamento será em USD. Inclua os detalhes da despesa original (conceito, valor, moeda). Retorne APENAS um objeto JSON com propriedade 'message'.");

        public static string ConsultaFaq(string locale)
            => LocaleHelper.Loc(locale,
                "OBLIGATORIO: Buscá en la base de conocimiento FAQ para responder la pregunta del usuario. Usá la herramienta Azure AI Search para encontrar la respuesta oficial. Si la encontrás, citá la cláusula o política relevante. Devolvé SOLO un objeto JSON con propiedad 'message' que contenga la respuesta.",
                "MANDATORY: Search the FAQ knowledge base to answer the user's question. Use the Azure AI Search tool to find the official answer. If found, cite the relevant clause or policy. Return ONLY a JSON object with a 'message' property containing the answer.",
                "OBRIGATÓRIO: Busque na base de conhecimento FAQ para responder à pergunta do usuário. Use a ferramenta Azure AI Search para encontrar a resposta oficial. Se encontrado, cite a cláusula ou política relevante. Retorne APENAS um objeto JSON com propriedade 'message' contendo a resposta.");

        public static string AnalizarProblemaReintegro(string locale)
            => LocaleHelper.Loc(locale,
                "Analizá el problema del usuario con su reintegro y sugerí próximos pasos. Sé breve.",
                "Analyze the user's issue with their reimbursement and provide next steps. Keep it brief.",
                "Analise o problema do usuário com o reembolso e forneça próximos passos. Seja breve.");

        public static string AnalizarProblemaGeneral(string locale)
            => LocaleHelper.Loc(locale,
                "Analizá el problema del usuario y sugerí próximos pasos. Sé breve. Devolvé SOLO un objeto JSON con propiedad 'message'.",
                "Analyze the user's issue and provide next steps. Keep it brief. Return ONLY a JSON object with a 'message' property.",
                "Analise o problema do usuário e forneça próximos passos. Seja breve. Retorne APENAS um objeto JSON com propriedade 'message'.");

        public static string ExplicarAprobacion(string locale)
            => LocaleHelper.Loc(locale,
                "Se encontró el reintegro aprobado, qué significa y cómo sigue?",
                "A reimbursement was approved. Explain what it means and what happens next.",
                "Um reembolso foi aprovado. Explique o que significa e quais são os próximos passos.");

        public static string ExplicarDenegacion(string locale)
            => LocaleHelper.Loc(locale,
                "Se encontró el reintegro denegado, qué significa y cómo sigue?",
                "A reimbursement was denied. Explain common reasons and what the customer can do next.",
                "Um reembolso foi negado. Explique motivos comuns e o que o cliente pode fazer.");

        public static string TipoCambioPagos(string locale)
            => LocaleHelper.Loc(locale,
                "Explicá cómo funciona el tipo de cambio en reintegros y cómo aplica.",
                "Explain exchange rate considerations for reimbursements and how it may apply.",
                "Explique como funciona o câmbio em reembolsos e como pode se aplicar.");

        public static string ProblemasPago(string locale)
            => LocaleHelper.Loc(locale,
                "Explicá problemas comunes con el pago de un reintegro y próximos pasos.",
                "Explain common payment issues for reimbursements and suggested next steps.",
                "Explique problemas comuns com pagamento de reembolso e próximos passos.");

        public static string TipoCambioReintegro(string locale) => TipoCambio(locale);
    }
}
