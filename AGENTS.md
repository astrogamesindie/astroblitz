# Instruções do projeto Astro Crawler

## Documentação obrigatória

- Antes de alterar o projeto, consultar `DOCUMENTACAO_TECNICA.md` na raiz.
- Sempre que modificar qualquer arquivo do projeto, atualizar também `DOCUMENTACAO_TECNICA.md` na mesma tarefa, antes da resposta final.
- Corrigir as seções afetadas e registrar no histórico a alteração, o motivo, os arquivos/assets envolvidos, a validação executada e as pendências. Para mudanças pequenas, registrar ao menos uma entrada no histórico.
- Aplicar essa regra também a mudanças em instruções, documentação, configuração, scripts, cenas, prefabs, Prefab Variants e ScriptableObjects.
- Distinguir comportamento implementado, propostas e resultados realmente verificados; não declarar testes ou medições que não foram executados.
- Consultas sem alteração de arquivos não exigem uma atualização artificial do documento.

## Revisão das alterações

- Preferir `apply_patch` para registrar as edições e apresentar o cartão nativo de arquivos alterados com os contadores de adições e remoções.
- Antes da resposta final, abrir a revisão nativa com `open_in_codex`, `placement="right"`, target de tipo `review` e `view="last-turn"`.
- Um retorno `queued` não confirma que o painel está visível; comunicar esse estado com precisão.
- Se a revisão nativa estiver indisponível, informar a limitação e apresentar as alterações em bloco Markdown `diff`.
- Não apresentar diff quando nenhum arquivo tiver sido alterado.
