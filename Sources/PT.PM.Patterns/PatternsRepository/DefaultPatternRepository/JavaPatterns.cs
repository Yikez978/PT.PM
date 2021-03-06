﻿using PT.PM.Common;
using PT.PM.Common.Nodes.Collections;
using PT.PM.Common.Nodes.Expressions;
using PT.PM.Common.Nodes.Tokens;
using PT.PM.Common.Nodes.Statements;
using PT.PM.Patterns.Nodes;
using System.Collections.Generic;
using PT.PM.Common.Nodes.Tokens.Literals;

namespace PT.PM.Patterns.PatternsRepository
{
    public partial class DefaultPatternRepository
    {
        public IEnumerable<Pattern> CreateJavaPatterns()
        {
            var patterns = new List<Pattern>();

            patterns.Add(new Pattern
            {
                Key = patternIdGenerator.NextId(),
                DebugInfo = "InadequateRsaPadding. Weak Encryption: Inadequate RSA Padding. ",
                Languages = LanguageFlags.Java,
                Data = new PatternNode
                {
                    Node = new InvocationExpression
                    {
                        Target = new MemberReferenceExpression
                        {
                            Name = new IdToken("getInstance"),
                            Target = new MemberReferenceExpression
                            {
                                Name = new IdToken("Cipher"),
                                Target = new MemberReferenceExpression
                                {
                                    Name = new IdToken("crypto"),
                                    Target = new IdToken("javax")
                                }
                            }
                        },
                        Arguments = new ArgsNode(new List<Expression>() { new PatternStringLiteral("^RSA/NONE/NoPadding$") })
                    }
                }
            });

            patterns.Add(new Pattern
            {
                Key = patternIdGenerator.NextId(),
                DebugInfo = "WeakCryptographicAlgorithm. Weak Encryption: Broken or Risky Cryptographic Algorithm" +
                    "https://cwe.mitre.org/data/definitions/327.html",
                Languages = LanguageFlags.Java,
                Data = new PatternNode
                {
                    Node = new InvocationExpression
                    {
                        Target = new MemberReferenceExpression
                        {
                            Name = new IdToken("getInstance"),
                            Target = new MemberReferenceExpression
                            {
                                Name = new IdToken("Cipher"),
                                Target = new MemberReferenceExpression
                                {
                                    Name = new IdToken("crypto"),
                                    Target = new IdToken("javax")
                                }
                            }
                        },
                        Arguments = new ArgsNode(new List<Expression>() { new PatternStringLiteral(@"DES") })
                    }
                }
            });

            patterns.Add(new Pattern
            {
                Key = patternIdGenerator.NextId(),
                DebugInfo = "OverlyBroadPath. Cookie Security: Overly Broad Path.",
                Languages = LanguageFlags.Java,
                Data = new PatternNode
                {
                    Node = new InvocationExpression
                    {
                        Target = new MemberReferenceExpression
                        {
                            Name = new IdToken("setPath"),
                            Target = new PatternIdToken(@"[cC]ookie")
                        },
                        Arguments = new ArgsNode(new List<Expression>() { new PatternStringLiteral { Text = "^/?$" } })
                    }
                }
            });

            patterns.Add(new Pattern
            {
                Key = patternIdGenerator.NextId(),
                DebugInfo = "OverlyBroadDomain Cookie Security: Overly Broad Domain.",
                Languages = LanguageFlags.Java,
                Data = new PatternNode
                {
                    Node = new InvocationExpression
                    {
                        Target = new MemberReferenceExpression
                        {
                            Name = new IdToken("setDomain"),
                            Target = new PatternIdToken { Id = @"[cC]ookie" }
                        },
                        Arguments = new ArgsNode(new List<Expression>() { new PatternStringLiteral(@"^.?[a-zA-Z0-9\-]+\.[a-zA-Z0-9\-]+$") })
                    }
                }
            });

            patterns.Add(new Pattern
            {
                Key = patternIdGenerator.NextId(),
                DebugInfo = "PoorSeeding.",
                Languages = LanguageFlags.Java,
                Data = new PatternNode
                {
                    Node = new InvocationExpression
                    {
                        Target = new MemberReferenceExpression
                        {
                            Name = new IdToken("setSeed"),
                            Target = new PatternExpression()
                        },
                        Arguments = new ArgsNode(new List<Expression>() { new PatternIntLiteral() })
                    }
                }
            });

            patterns.Add(new Pattern
            {
                Key = patternIdGenerator.NextId(),
                DebugInfo = "WeakCryptographicHash.",
                Languages = LanguageFlags.Java,
                Data = new PatternNode
                {
                    Node = new InvocationExpression
                    {
                        Target = new MemberReferenceExpression
                        {
                            Name = new IdToken("getInstance"),
                            Target = new IdToken("MessageDigest")
                        },
                        Arguments = new ArgsNode(new List<Expression>() { new PatternStringLiteral("MD5|SHA-1") })
                    }
                }
            });

            patterns.Add(new Pattern
            {
                Key = patternIdGenerator.NextId(),
                DebugInfo = "AndroidPermissionCheck. Often Misused: Android Permission Check.",
                Languages = LanguageFlags.Java,
                Data = new PatternNode
                {
                    Node = new InvocationExpression
                    {
                        Target = new MemberReferenceExpression
                        {
                            Name = new PatternIdToken("^(checkCallingOrSelfPermission|checkCallingOrSelfUriPermission)$"),
                            Target = new PatternExpression()
                        },
                        Arguments = new PatternExpressions(new PatternMultipleExpressions())
                    }
                }
            });

            patterns.Add(new Pattern
            {
                Key = patternIdGenerator.NextId(),
                DebugInfo = "AndroidHostnameVerificationDisabled. Insecure SSL: Android Hostname Verification Disabled.",
                Languages = LanguageFlags.Java,
                Data = new PatternNode
                {
                    Node = new PatternVarDef
                    {
                        Values = new List<Expression>()
                    {
                        new MemberReferenceExpression
                        {
                            Name = new IdToken("ALLOW_ALL_HOSTNAME_VERIFIER"),
                            Target = new IdToken("SSLSocketFactory")
                        },
                        new ObjectCreateExpression
                        {
                            Type = new TypeToken { TypeText = "AllowAllHostnameVerifier" },
                            Arguments = new PatternExpressions(new PatternMultipleExpressions())
                        }
                    }
                    }
                }
            });

            patterns.Add(new Pattern
            {
                Key = patternIdGenerator.NextId(),
                DebugInfo = "SAXReaderExternalEntity",
                Languages = LanguageFlags.Java,
                Data = new PatternNode
                {
                    Node = new InvocationExpression
                    {
                        Arguments = new ArgsNode
                        {
                        Collection = new List<Expression>()
                        {
                            new PatternExpression(new PatternStringLiteral(), true)
                        }
                        },
                        Target = new MemberReferenceExpression
                        {
                            Name = new IdToken("read"),
                            Target = new ObjectCreateExpression
                            {
                                Type = new TypeToken { TypeText = "SAXReader" },
                                Arguments = new ArgsNode()
                            }
                        }
                    }
                }
            });

            patterns.Add(new Pattern
            {
                Key = patternIdGenerator.NextId(),
                DebugInfo = "XmlExternalEntity",
                Languages = LanguageFlags.Java,
                Data = new PatternNode
                {
                    Node = new InvocationExpression
                    {
                        Arguments = new ArgsNode
                        {
                            Collection = new List<Expression>()
                        {
                            new PatternExpression(new PatternStringLiteral(), true)
                        }
                        },
                        Target = new MemberReferenceExpression
                        {
                            Name = new IdToken("parse"),
                            Target = new ObjectCreateExpression
                            {
                                Type = new TypeToken { TypeText = "XMLUtil" },
                                Arguments = new ArgsNode()
                            }
                        }
                    }
                }
            });

            patterns.Add(new Pattern
            {
                Key = patternIdGenerator.NextId(),
                DebugInfo = "StickyBroadcast. Android Bad Practices: Sticky Broadcast.",
                Languages = LanguageFlags.Java,
                Data = new PatternNode
                {
                    Node = new InvocationExpression
                    {
                        Arguments = new ArgsNode
                        {
                            Collection = new List<Expression>() { new PatternExpression() }
                        },
                        Target = new MemberReferenceExpression
                        {
                            Name = new IdToken("sendStickyBroadcast"),
                            Target = new PatternExpression()
                        }
                    }
                }
            });

            patterns.Add(new Pattern
            {
                Key = patternIdGenerator.NextId(),
                DebugInfo = "SendStickyBroadcastAsUser. Android Bad Practices: Sticky Broadcast.",
                Languages = LanguageFlags.Java,
                Data = new PatternNode
                {
                    Node = new InvocationExpression
                    {
                        Arguments = new ArgsNode
                        {
                            Collection = new List<Expression>() { new PatternExpression(), new PatternExpression() }
                        },
                        Target = new MemberReferenceExpression
                        {
                            Name = new IdToken("sendStickyBroadcastAsUser"),
                            Target = new PatternExpression()
                        }
                    }
                }
            });

            // TODO: implement "createSocket"
            patterns.Add(new Pattern
            {
                Key = patternIdGenerator.NextId(),
                DebugInfo = "InsecureSSL. Insecure SSL: Android Socket.",
                Languages = LanguageFlags.Java,
                Data = new PatternNode
                {
                    Node = new InvocationExpression
                    {
                        Arguments = new ArgsNode
                        {
                            Collection = new List<Expression>()
                        {
                            new PatternExpression(),
                            new PatternExpression()
                        }
                        },
                        Target = new MemberReferenceExpression
                        {
                            Name = new IdToken { Id = "getInsecure" },
                            Target = new PatternExpression()
                        }
                    }
                }
            });

            patterns.Add(new Pattern
            {
                Key = patternIdGenerator.NextId(),
                DebugInfo = "HardcodedSalt. Weak Cryptographic Hash: Hardcoded Salt.",
                Languages = LanguageFlags.Java,
                Data = new PatternNode
                {
                    Node = new InvocationExpression
                    {
                        Arguments = new ArgsNode
                        {
                            Collection = new List<Expression>() {
                            new PatternExpression(),
                            new PatternStringLiteral()
                        }
                        },
                        Target = new MemberReferenceExpression
                        {
                            Name = new IdToken { Id = "hash" },
                            Target = new PatternExpression()
                        }
                    }
                }
            });

            patterns.Add(new Pattern
            {
                Key = patternIdGenerator.NextId(),
                DebugInfo = "MissingReceiverPermission. The program sends a broadcast without specifying the receiver permission. " +
                              "Broadcasts sent without the receiver permission are accessible to any receiver. If these broadcasts contain sensitive data or reach a malicious receiver, the application may be compromised.",
                Languages = LanguageFlags.Java,
                Data = new PatternNode
                {
                    Node = new InvocationExpression
                    {
                        Arguments = new ArgsNode
                        {
                            Collection = new List<Expression>() {
                            new PatternExpression()
                        }
                        },
                        Target = new MemberReferenceExpression
                        {
                            Name = new IdToken { Id = "sendBroadcast" },
                            Target = new PatternExpression()
                        }
                    }
                }
            });

            patterns.Add(new Pattern
            {
                Key = patternIdGenerator.NextId(),
                DebugInfo = "MissingBroadcasterPermission. The program registers a receiver without specifying the broadcaster permission. " +
                    "Receiver registered without the broadcaster permission will receive messages from any broadcaster. " +
                    "If these messages contain malicious data or come from a malicious broadcaster, the application may be compromised. " +
                    "Use this form: public abstract Intent registerReceiver (BroadcastReceiver receiver, IntentFilter filter, String broadcastPermission, Handler scheduler)",
                Languages = LanguageFlags.Java,
                Data = new PatternNode
                {
                    Node = new InvocationExpression
                    {
                        Arguments = new ArgsNode
                        {
                            Collection = new List<Expression>() {
                            new PatternExpression(),
                            new PatternExpression()
                        }
                        },
                        Target = new MemberReferenceExpression
                        {
                            Name = new IdToken { Id = "registerReceiver" },
                            Target = new PatternExpression()
                        }
                    }
                }
            });

            var cookieVar = new PatternVarDef
            {
                Id = "cookie",
                Values = new List<Expression>() { new PatternIdToken() }
            };
            patterns.Add(new Pattern
            {
                Key = patternIdGenerator.NextId(),
                DebugInfo = "CookieNotSentOverSSL. Cookie Security: Cookie not Sent Over SSL. ",
                Languages = LanguageFlags.Java,
                Data = new PatternNode
                {
                    Vars = new List<PatternVarDef> { cookieVar },
                    Node = new PatternStatements
                    {
                        Statements = new List<Statement>()
                    {
                        new ExpressionStatement(new VariableDeclarationExpression
                        {
                            Type = new TypeToken() { TypeText = "Cookie" },
                            Variables = new List<AssignmentExpression>
                            {
                                new AssignmentExpression
                                {
                                    Left = new PatternVarRef(cookieVar),
                                    Right = new ObjectCreateExpression
                                    {
                                        Type = new TypeToken { TypeText = "Cookie" },
                                        Arguments = new PatternExpressions(new PatternMultipleExpressions())
                                    },
                                }
                            }
                        }),

                        new PatternMultipleStatements(),

                        new PatternStatement(new ExpressionStatement(new InvocationExpression
                        {
                            Arguments = new ArgsNode
                            {
                                Collection = new List<Expression>() { new BooleanLiteral { Value = true } }
                            },
                            Target = new MemberReferenceExpression
                            {
                                Name = new IdToken { Id = "setSecure" },
                                Target = new PatternVarRef(cookieVar)
                            }
                        }), true),

                        new PatternMultipleStatements(),

                        new ExpressionStatement(new InvocationExpression
                        {
                            Arguments = new ArgsNode
                            {
                                Collection = new List<Expression>() { new PatternVarRef(cookieVar) }
                            },
                            Target = new MemberReferenceExpression
                            {
                                Name = new IdToken { Id = "addCookie" },
                                Target = new PatternExpression()
                            }
                        })
                    }
                    }
                }
            });

            return patterns;
        }
    }
}
