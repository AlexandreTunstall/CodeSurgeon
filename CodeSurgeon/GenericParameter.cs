using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CodeSurgeon
{
    public class GenericParameter : IResolvable<GenericParam>
    {
        public ushort Number { get; }
        public GenericParamAttributes Attributes { get; }
        public UTF8String Name { get; }
        public IReadOnlyList<GenericParameterConstraint> Constraints { get; }

        public GenericParameter(ushort number, GenericParamAttributes attributes, UTF8String name, params GenericParameterConstraint[] constraints) : this(number, attributes, name, (IEnumerable<GenericParameterConstraint>)constraints) { }
        public GenericParameter(ushort number, GenericParamAttributes attributes, UTF8String name, IEnumerable<GenericParameterConstraint> constraints)
        {
            Number = number;
            Attributes = attributes;
            Name = name;
            Constraints = constraints.ToList();
        }

        public GenericParam Resolve(ISearchContext context)
        {
            GenericParam result = new GenericParamUser(Number, Attributes, Name);
            foreach (GenericParameterConstraint constraint in Constraints) result.GenericParamConstraints.Add(context.Get(constraint));
            return result;
        }
    }

    public class GenericParameterConstraint : IResolvable<GenericParamConstraint>
    {
        public ITypeReference<ITypeDefOrRef> Constraint { get; }

        public GenericParameterConstraint(ITypeReference<ITypeDefOrRef> constraint) => Constraint = constraint;

        public GenericParamConstraint Resolve(ISearchContext context) => new GenericParamConstraintUser(context.Get(Constraint));
    }

    public class GenericParamComparer : IEqualityComparer<GenericParam>, IEqualityComparer<GenericParamConstraint>
    {
        private readonly TypeEqualityComparer typeComparer;

        public GenericParamComparer(TypeEqualityComparer typeComparer = null) => this.typeComparer = typeComparer ?? new TypeEqualityComparer(default);

        public bool Equals(GenericParam x, GenericParam y) => ReferenceEquals(x, y) || !(x is null) && !(y is null) && x.Number.Compare(y.Number) && Enumerable.SequenceEqual(x.GenericParamConstraints, y.GenericParamConstraints, this);

        public bool Equals(GenericParamConstraint x, GenericParamConstraint y) => ReferenceEquals(x, y) || !(x is null) && !(y is null) && typeComparer.Equals(x.Constraint, y.Constraint);

        // Technically legal; horrible practice
        public int GetHashCode(GenericParam obj) => 0;
        public int GetHashCode(GenericParamConstraint obj) => 0;
    }
}
