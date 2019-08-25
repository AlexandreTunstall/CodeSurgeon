using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CodeSurgeon
{
    public interface ICallSignature<out TSymbol> : IResolvable<TSymbol>, IEquatable<ICallSignature<CallingConventionSig>> where TSymbol : CallingConventionSig
    {
        CallingConvention CallingConvention { get; }
    }

    public abstract class AbstractCallSignature<TSymbol> : ICallSignature<TSymbol> where TSymbol : CallingConventionSig
    {
        public CallingConvention CallingConvention { get; }

        private protected AbstractCallSignature(CallingConvention callingConvention) => CallingConvention = callingConvention;

        public abstract TSymbol Resolve(ISearchContext context);

        public abstract bool Equals(ICallSignature<CallingConventionSig> other);
    }

    public sealed class FieldSignature : ICallSignature<FieldSig>, IEquatable<FieldSignature>
    {
        public CallingConvention CallingConvention => CallingConvention.Field;
        public ITypeSignature<TypeSig> Type { get; }

        public FieldSignature(ITypeSignature<TypeSig> type) => Type = type;

        public FieldSig Resolve(ISearchContext context) => new FieldSig(context.Get(Type));

        public bool Equals(ICallSignature<CallingConventionSig> other) => Equals(other as FieldSignature);
        public bool Equals(FieldSignature other) => !(other is null) && Type.Compare(other.Type);
    }

    public interface IMethodBaseSignature<out TSymbol> : ICallSignature<TSymbol>, IEquatable<IMethodBaseSignature<MethodBaseSig>> where TSymbol : MethodBaseSig
    {
        uint GenParamCount { get; }
        ITypeSignature<TypeSig> RetType { get; }
        IEnumerable<ITypeSignature<TypeSig>> Parameters { get; }
        IEnumerable<ITypeSignature<TypeSig>> ParamsAfterSentinel { get; }
    }

    public abstract class AbstractMethodBaseSignature<TSymbol> : AbstractCallSignature<TSymbol>, IMethodBaseSignature<TSymbol> where TSymbol : MethodBaseSig
    {
        public uint GenParamCount { get; }
        public ITypeSignature<TypeSig> RetType { get; }
        public IEnumerable<ITypeSignature<TypeSig>> Parameters { get; }
        public IEnumerable<ITypeSignature<TypeSig>> ParamsAfterSentinel { get; }
        
        private protected AbstractMethodBaseSignature(CallingConvention callingConvention, uint genParamCount, ITypeSignature<TypeSig> retType, IEnumerable<ITypeSignature<TypeSig>> parameters, IEnumerable<ITypeSignature<TypeSig>> paramsAfterSentinel) : base(callingConvention)
        {
            RetType = retType;
            Parameters = parameters;
            GenParamCount = genParamCount;
            ParamsAfterSentinel = paramsAfterSentinel;
        }

        public override bool Equals(ICallSignature<CallingConventionSig> other) => Equals(other as MethodBaseSig);
        public virtual bool Equals(IMethodBaseSignature<MethodBaseSig> other) => !(other is null) && CallingConvention == other.CallingConvention && Parameters.Compare(other.Parameters);
    }

    public sealed class MethodSignature : AbstractMethodBaseSignature<MethodSig>, IEquatable<MethodSignature>
    {
        public MethodSignature(CallingConvention callingConvention, uint genParamCount, ITypeSignature<TypeSig> retType, IEnumerable<ITypeSignature<TypeSig>> parameters = null, IEnumerable<ITypeSignature<TypeSig>> paramsAfterSentinel = null) : base(callingConvention, genParamCount, retType, parameters, paramsAfterSentinel) { }

        public override MethodSig Resolve(ISearchContext context) => new MethodSig(CallingConvention, GenParamCount, context.Get(RetType), Parameters?.Select(context.Get).ToList(), ParamsAfterSentinel?.Select(context.Get).ToList());

        public override bool Equals(IMethodBaseSignature<MethodBaseSig> other) => Equals(other as MethodSignature);
        public bool Equals(MethodSignature other) => base.Equals(other);

        public override bool Equals(object obj) => obj is MethodSignature other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = 617042201;
                hashCode = hashCode * -1521134295 + CallingConvention.GetHashCode();
                hashCode = hashCode * -1521134295 + Parameters.Count().GetHashCode(); // Parameters.Aggregate(0, (v, s) => v + 97 * s.GetHashCode());
                return hashCode;
            }
        }
    }

    public sealed class PropertySignature : AbstractMethodBaseSignature<PropertySig>, IEquatable<PropertySignature>
    {
        public bool HasThis => CallingConvention.HasFlag(CallingConvention.HasThis);

        public PropertySignature(bool hasThis, ITypeSignature<TypeSig> retType, IEnumerable<ITypeSignature<TypeSig>> parameters = null) : base(CallingConvention.Property | (hasThis ? CallingConvention.HasThis : 0), 0u, retType, parameters, null) { }

        public override PropertySig Resolve(ISearchContext context) => new PropertySig(HasThis, context.Get(RetType), Parameters?.Select(context.Get).ToArray());

        public override bool Equals(IMethodBaseSignature<MethodBaseSig> other) => Equals(other as PropertySignature);
        public bool Equals(PropertySignature other) => base.Equals(other);
    }

    public sealed class LocalSignature : ICallSignature<LocalSig>, IEquatable<LocalSignature>
    {
        public CallingConvention CallingConvention => CallingConvention.LocalSig;
        public IEnumerable<ITypeSignature<TypeSig>> Locals { get; }

        public LocalSignature(IEnumerable<ITypeSignature<TypeSig>> locals) => Locals = locals;

        public LocalSig Resolve(ISearchContext context) => new LocalSig(Locals?.Select(context.Get).ToList());

        public bool Equals(ICallSignature<CallingConventionSig> other) => Equals(other as LocalSignature);
        public bool Equals(LocalSignature other) => !(other is null) && Locals.Compare(other.Locals);
    }

    public sealed class GenericInstMethodSignature : ICallSignature<GenericInstMethodSig>, IEquatable<GenericInstMethodSignature>
    {
        public CallingConvention CallingConvention => CallingConvention.GenericInst;
        public IEnumerable<ITypeSignature<TypeSig>> Args { get; }

        public GenericInstMethodSignature(IEnumerable<ITypeSignature<TypeSig>> args) => Args = args;

        public GenericInstMethodSig Resolve(ISearchContext context) => new GenericInstMethodSig(Args.Select(context.Get).ToList());

        public bool Equals(ICallSignature<CallingConventionSig> other) => Equals(other as GenericInstMethodSignature);
        public bool Equals(GenericInstMethodSignature other) => !(other is null) && Args.Compare(other.Args);
    }
}
